namespace SupportDesk.Application.Contracts.Tickets;

public sealed record TicketEscalationDto(
    int Id,
    int TicketId,
    int FromPriority,
    int ToPriority,
    int? FromAgentId,
    int? ToAgentId,
    DateTime? PreviousDueAtUtc,
    DateTime? NewDueAtUtc,
    string Reason,
    string EscalatedBy,
    DateTime EscalatedAtUtc);

public sealed record EscalateTicketResponse(
    TicketDetailDto Ticket,
    TicketEscalationDto Escalation);