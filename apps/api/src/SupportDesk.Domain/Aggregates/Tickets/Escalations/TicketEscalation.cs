namespace SupportDesk.Domain.Aggregates.Tickets.Escalations;

public sealed class TicketEscalation
{
    private TicketEscalation()
    {
        // For EF Core.
    }

    public int Id { get; private set; }

    public int TicketId { get; private set; }

    public int FromPriority { get; private set; }

    public int ToPriority { get; private set; }

    public int? FromAgentId { get; private set; }

    public int? ToAgentId { get; private set; }

    public DateTime? PreviousDueAtUtc { get; private set; }

    public DateTime? NewDueAtUtc { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public string EscalatedBy { get; private set; } = string.Empty;

    public DateTime EscalatedAtUtc { get; private set; }

    public static TicketEscalation Create(
        int ticketId,
        int fromPriority,
        int toPriority,
        int? fromAgentId,
        int? toAgentId,
        DateTime? previousDueAtUtc,
        DateTime? newDueAtUtc,
        string reason,
        string escalatedBy,
        DateTime escalatedAtUtc)
    {
        if (ticketId <= 0)
            throw new ArgumentOutOfRangeException(nameof(ticketId));

        if (string.IsNullOrWhiteSpace(reason) ||
            reason.Trim().Length < 5 ||
            reason.Trim().Length > 500)
            throw new ArgumentException(
                "Reason must contain between 5 and 500 characters.", nameof(reason));

        if (string.IsNullOrWhiteSpace(escalatedBy) ||
            escalatedBy.Trim().Length > 100)
            throw new ArgumentException(
                "EscalatedBy is required and must not exceed 100 characters.",
                nameof(escalatedBy));

        return new TicketEscalation
        {
            TicketId = ticketId,
            FromPriority = fromPriority,
            ToPriority = toPriority,
            FromAgentId = fromAgentId,
            ToAgentId = toAgentId,
            PreviousDueAtUtc = previousDueAtUtc,
            NewDueAtUtc = newDueAtUtc,
            Reason = reason.Trim(),
            EscalatedBy = escalatedBy.Trim(),
            EscalatedAtUtc = escalatedAtUtc
        };
    }
}