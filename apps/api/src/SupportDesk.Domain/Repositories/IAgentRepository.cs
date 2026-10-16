using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Domain.Repositories;

/// <summary>
/// Loads agents and selects eligible agents for ticket assignment.
/// </summary>
public interface IAgentRepository
{
    Task<Agent?> GetByIdAsync(int id, CancellationToken ct);

    Task<Agent?> GetByEmailAsync(string email, CancellationToken ct);

    Task<Agent?> GetBestAvailableAsync(
        int categoryId,
        bool requiresSpecialist,
        CancellationToken ct);

        Task<bool> IsEligibleForTicketAsync(
    int agentId,
    int categoryId,
    bool requiresSpecialist,
    int ticketId,
    CancellationToken ct);
}
