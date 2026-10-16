
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Options;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

/// <summary>
/// Raises a ticket, calculates its SLA deadline, and assigns an eligible agent.
/// </summary>
public sealed class RaiseTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentRepository agents,
    IUnitOfWork unitOfWork,
    IClock clock,
    IOptions<SlaOptions> slaOptions,
    GetTicketQueryHandler getTicket,
    ILogger<RaiseTicketCommandHandler> logger)
{
    public async Task<TicketDetailDto> HandleAsync(
        CreateTicketRequest request,
        CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(
            request.CustomerId, ct);

        if (customer is null)
        {
            throw new NotFoundException("Customer", request.CustomerId);
        }

        var category = await categories.GetByIdAsync(
            request.CategoryId, ct);

        if (category is null)
        {
            throw new NotFoundException("Category", request.CategoryId);
        }

        var now = clock.UtcNow;
        var options = slaOptions.Value;

        var priority = category.ForcesCriticalPriority
            ? TicketPriority.Critical
            : request.RequestedPriority ?? TicketPriority.Medium;

        if (!options.BaseWindowHours.TryGetValue(
                priority.ToString(), out var baseHours))
        {
            throw new InvalidOperationException(
                $"No SLA window is configured for priority {priority}.");
        }

        var multiplier = customer.Tier == CustomerTier.Premium
            ? options.PremiumCustomerMultiplier
            : 1.0;

        var windowHours = Math.Max(
            options.MinimumWindowHours,
            baseHours * multiplier);

        var dueAt = now.AddHours(windowHours);

        var ticket = Ticket.Raise(
            await tickets.NextReferenceAsync(ct),
            request.Title,
            request.Description,
            request.CustomerId,
            request.CategoryId,
            priority,
            now);

        // The SLA window starts when the ticket is raised.
        ticket.SetDueAt(dueAt, now);

        var assignedAgent = await agents.GetBestAvailableAsync(
            category.Id,
            category.RequiresSpecialist,
            ct);

        if (assignedAgent is not null)
        {
            ticket.AssignTo(assignedAgent.Id, now);
        }

        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var priorityReason = category.ForcesCriticalPriority
            ? "Priority set to Critical because the category requires it."
            : $"Priority set to {priority}.";

        var slaReason = customer.Tier == CustomerTier.Premium
            ? $"Premium SLA window: {windowHours:0.##} hours."
            : $"SLA window: {windowHours:0.##} hours.";

        var assignmentReason = assignedAgent is not null
            ? $"Assigned to {assignedAgent.FullName}."
            : category.RequiresSpecialist
                ? "No eligible active specialist has available capacity; ticket remains unassigned."
                : "No eligible active agent has available capacity; ticket remains unassigned.";

        logger.LogInformation(
            "Ticket {Reference} raised with priority {Priority}, SLA start {SlaStartAtUtc}, and due date {DueAtUtc}.",
            ticket.Reference,
            ticket.Priority,
            ticket.SlaWindowStartAtUtc,
            ticket.DueAtUtc);

        var result = await getTicket.HandleAsync(ticket.Id, ct);

        result.TriageReason =
            $"{priorityReason} {slaReason} {assignmentReason}";

        return result;
    }
}
