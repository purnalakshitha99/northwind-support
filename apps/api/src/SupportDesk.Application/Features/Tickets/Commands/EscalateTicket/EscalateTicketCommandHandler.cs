
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Options;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Aggregates.Tickets.Escalations;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ITicketEscalationRepository escalations,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentRepository agents,
    IUnitOfWork unitOfWork,
    IClock clock,
    IOptions<SlaOptions> slaOptions,
    GetTicketQueryHandler getTicket,
    ILogger<EscalateTicketCommandHandler> logger)
{
    public async Task<EscalateTicketResponse> HandleAsync(
        int ticketId,
        EscalateTicketRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) ||
            request.Reason.Trim().Length is < 5 or > 500)
        {
            throw new ArgumentException(
                "Reason must contain between 5 and 500 characters.");
        }

        if (string.IsNullOrWhiteSpace(request.EscalatedBy) ||
            request.EscalatedBy.Trim().Length > 100)
        {
            throw new ArgumentException(
                "EscalatedBy is required and must not exceed 100 characters.");
        }

        var ticket = await tickets.GetByIdAsync(ticketId, ct)
            ?? throw new NotFoundException("Ticket", ticketId);

        if (ticket.Priority == TicketPriority.Critical)
        {
            throw new ConflictException(
                $"Ticket {ticket.Reference} is already Critical and cannot be escalated.");
        }

        if (ticket.Status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            throw new ConflictException(
                $"Ticket {ticket.Reference} is {ticket.Status} and cannot be escalated.");
        }

        var customer = await customers.GetByIdAsync(
            ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);

        var category = await categories.GetByIdAsync(
            ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);

        var now = clock.UtcNow;

        var previousPriority = ticket.Priority;
        var previousAgentId = ticket.AssignedAgentId;
        var previousDueAt = ticket.DueAtUtc;

        ticket.EscalatePriority(now);

        var options = slaOptions.Value;

        if (!options.BaseWindowHours.TryGetValue(
                ticket.Priority.ToString(), out var baseHours))
        {
            throw new InvalidOperationException(
                $"No SLA window is configured for priority {ticket.Priority}.");
        }

        var multiplier = customer.Tier == CustomerTier.Premium
            ? options.PremiumCustomerMultiplier
            : 1.0;

        var windowHours = Math.Max(
            options.MinimumWindowHours,
            baseHours * multiplier);

        var newDueAt = now.AddHours(windowHours);

        // Escalation starts a new SLA window at the escalation time.
        ticket.SetDueAt(newDueAt, now);

        var keepCurrentAgent = false;

        if (previousAgentId.HasValue)
        {
            keepCurrentAgent = await agents.IsEligibleForTicketAsync(
                previousAgentId.Value,
                category.Id,
                category.RequiresSpecialist,
                ticket.Id,
                ct);
        }

        if (!keepCurrentAgent)
        {
            var newAgent = await agents.GetBestAvailableAsync(
                category.Id,
                category.RequiresSpecialist,
                ct);

            ticket.AssignTo(newAgent?.Id, now);
        }

        var history = TicketEscalation.Create(
            ticket.Id,
            previousPriority,
            ticket.Priority,
            previousAgentId,
            ticket.AssignedAgentId,
            previousDueAt,
            newDueAt,
            request.Reason,
            request.EscalatedBy,
            now);

        await escalations.AddAsync(history, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {TicketId} escalated from {FromPriority} to {ToPriority}. SLA start {SlaStartAtUtc}, new due date {DueAtUtc}.",
            ticket.Id,
            previousPriority,
            ticket.Priority,
            ticket.SlaWindowStartAtUtc,
            ticket.DueAtUtc);

        var updatedTicket = await getTicket.HandleAsync(ticket.Id, ct);

        var historyDto = new TicketEscalationDto(
            history.Id,
            history.TicketId,
            history.FromPriority,
            history.ToPriority,
            history.FromAgentId,
            history.ToAgentId,
            history.PreviousDueAtUtc,
            history.NewDueAtUtc,
            history.Reason,
            history.EscalatedBy,
            history.EscalatedAtUtc);

        return new EscalateTicketResponse(updatedTicket, historyDto);
    }
}
