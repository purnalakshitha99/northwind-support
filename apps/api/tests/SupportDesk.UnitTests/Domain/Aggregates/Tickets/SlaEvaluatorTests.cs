using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaEvaluatorTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    [Fact]
    public void Evaluate_WithoutADueDate_IsNotApplicable() =>
        Assert.Equal(SlaStatus.NotApplicable, SlaEvaluator.Evaluate(null, null, Now));

    [Fact]
    public void Evaluate_ResolvedOnTime_IsMet() =>
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Now, Now, Now.AddDays(1)));

    [Fact]
    public void Evaluate_ResolvedExactlyAtDueDate_IsMet() =>
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Now, Now, Now));

    [Fact]
    public void Evaluate_ResolvedLate_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now, Now.AddMinutes(1), Now));

    [Fact]
    public void Evaluate_OpenAndPastDue_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now.AddMinutes(-1), null, Now));

    [Fact]
    public void Evaluate_OpenAndBeforeDue_WithoutWindowStart_IsWithinSla() =>
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(Now.AddHours(1), null, Now));

    [Fact]
    public void Evaluate_OpenAndMoreThan25PercentRemaining_IsWithinSla()
    {
        // 10 hour window (Now to +10h). Currently at Now + 5h (50% remaining).
        var windowStart = Now;
        var dueAt = Now.AddHours(10);
        var evalTime = Now.AddHours(5); // 5h remaining out of 10h = 50%

        Assert.Equal(
            SlaStatus.WithinSla,
            SlaEvaluator.Evaluate(dueAt, null, windowStart, evalTime, 25));
    }

    [Fact]
    public void Evaluate_OpenAndAtExact25PercentRemainingBoundary_IsAtRisk()
    {
        // 100 minutes window. 25 minutes remaining = exactly 25%.
        var windowStart = Now;
        var dueAt = Now.AddMinutes(100);
        var evalTime = Now.AddMinutes(75); // 25 minutes left

        Assert.Equal(
            SlaStatus.AtRisk,
            SlaEvaluator.Evaluate(dueAt, null, windowStart, evalTime, 25));
    }

    [Fact]
    public void Evaluate_OpenAndLessThan25PercentRemaining_IsAtRisk()
    {
        // 100 minutes window. 10 minutes remaining = 10%.
        var windowStart = Now;
        var dueAt = Now.AddMinutes(100);
        var evalTime = Now.AddMinutes(90); // 10 minutes left

        Assert.Equal(
            SlaStatus.AtRisk,
            SlaEvaluator.Evaluate(dueAt, null, windowStart, evalTime, 25));
    }

    [Fact]
    public void Evaluate_OpenAndJustAbove25PercentRemaining_IsWithinSla()
    {
        // 100 minutes window. 26 minutes remaining = 26%.
        var windowStart = Now;
        var dueAt = Now.AddMinutes(100);
        var evalTime = Now.AddMinutes(74); // 26 minutes left

        Assert.Equal(
            SlaStatus.WithinSla,
            SlaEvaluator.Evaluate(dueAt, null, windowStart, evalTime, 25));
    }
}
