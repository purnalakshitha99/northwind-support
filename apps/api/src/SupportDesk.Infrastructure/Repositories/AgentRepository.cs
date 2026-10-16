using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class AgentRepository(SupportDbContext db) : IAgentRepository
{
    public Task<Agent?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Set<Agent>()
            .Include(a => a.Specializations)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<Agent?> GetBestAvailableAsync(
        int categoryId,
        bool requiresSpecialist,
        CancellationToken ct)
    {
        var agents = db.Set<Agent>()
            .AsNoTracking()
            .Where(a => a.IsActive);

        if (requiresSpecialist)
        {
            agents = agents.Where(a =>
                a.Specializations.Any(s => s.CategoryId == categoryId));
        }

        return agents
            .Where(a =>
                db.Set<Ticket>().Count(t =>
                    t.AssignedAgentId == a.Id &&
                    t.Status != TicketStatus.Resolved &&
                    t.Status != TicketStatus.Closed)
                < a.MaxOpenTickets)
            .OrderBy(a => db.Set<Ticket>().Count(t =>
                t.AssignedAgentId == a.Id &&
                t.Status != TicketStatus.Resolved &&
                t.Status != TicketStatus.Closed))
            .ThenBy(a => a.Id)
            .Include(a => a.Specializations)
            .FirstOrDefaultAsync(ct);
    }
    public async Task<bool> IsEligibleForTicketAsync(
    int agentId,
    int categoryId,
    bool requiresSpecialist,
    int ticketId,
    CancellationToken ct)
{
    var agent = await db.Set<Agent>()
        .Include(a => a.Specializations)
        .FirstOrDefaultAsync(a => a.Id == agentId, ct);

    if (agent is null || !agent.IsActive)
        return false;

    if (requiresSpecialist &&
        !agent.Specializations.Any(s => s.CategoryId == categoryId))
        return false;

    var openTicketCount = await db.Set<Ticket>()
        .CountAsync(t =>
            t.AssignedAgentId == agentId &&
            t.Id != ticketId &&
            t.Status != TicketStatus.Resolved &&
            t.Status != TicketStatus.Closed,
            ct);

    return openTicketCount < agent.MaxOpenTickets;
}
}