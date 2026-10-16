namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Calculates SLA status from ticket dates and the current time.
/// </summary>
public static class SlaEvaluator
{
    public static SlaStatus Evaluate(
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime nowUtc) =>
        Evaluate(dueAtUtc, resolvedAtUtc, null, nowUtc, 25);

    public static SlaStatus Evaluate(
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime? windowStartUtc,
        DateTime nowUtc,
        double atRiskThresholdPercent = 25)
    {
        if (dueAtUtc is null)
            return SlaStatus.NotApplicable;

        if (resolvedAtUtc is not null)
        {
            return resolvedAtUtc <= dueAtUtc
                ? SlaStatus.Met
                : SlaStatus.Breached;
        }

        if (nowUtc > dueAtUtc.Value)
            return SlaStatus.Breached;

        // Older/seeded tickets can have a due date but no recorded SLA window start.
        // Match the list-query filter: consider them WithinSla until their deadline.
        if (windowStartUtc is null)
            return SlaStatus.WithinSla;

        var totalWindow = dueAtUtc.Value - windowStartUtc.Value;

        if (totalWindow <= TimeSpan.Zero)
            return SlaStatus.WithinSla;

        var remaining = dueAtUtc.Value - nowUtc;
        var remainingPercent =
            remaining.TotalSeconds / totalWindow.TotalSeconds * 100.0;

        return remainingPercent <= atRiskThresholdPercent
            ? SlaStatus.AtRisk
            : SlaStatus.WithinSla;
    }
}
