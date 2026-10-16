using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketCommandHandlerTests
{
    private static EscalateTicketRequest Request(
        string reason = "Client urgently requires an update on the ongoing outage.",
        string escalatedBy = "Alex Turner") =>
        new(reason, escalatedBy);

    [Fact]
    public async Task HandleAsync_RaisesPriorityByOneLevel()
    {
        var context = new TicketCommandTestContext();
        var ticket = new TicketBuilder()
            .WithPriority(TicketPriority.Medium)
            .Build();

        context.ExistingTicket(ticket);

        var response = await context.EscalateTicket.HandleAsync(
            ticket.Id,
            Request(),
            CancellationToken.None);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(TicketPriority.Medium, response.Escalation.FromPriority);
        Assert.Equal(TicketPriority.High, response.Escalation.ToPriority);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_RecomputesDueDateFromMomentOfEscalation()
    {
        var context = new TicketCommandTestContext();
        var ticket = new TicketBuilder()
            .WithPriority(TicketPriority.Medium)
            .Build();

        context.ExistingTicket(ticket);

        // Advance the clock by 2 hours before escalating
        context.Clock.Advance(TimeSpan.FromHours(2));
        var escalationTime = context.Clock.UtcNow;

        // Base window for High = 8 hours. Premium customer gets 50% = 4 hours.
        await context.EscalateTicket.HandleAsync(
            ticket.Id,
            Request(),
            CancellationToken.None);

        var expectedDueAt = escalationTime.AddHours(4);
        Assert.Equal(expectedDueAt, ticket.DueAtUtc);
        Assert.Equal(escalationTime, ticket.SlaWindowStartAtUtc);
    }

    [Fact]
    public async Task HandleAsync_WhenCurrentAgentIsStillEligible_KeepsCurrentAgent()
    {
        var context = new TicketCommandTestContext();
        var ticket = new TicketBuilder()
            .WithPriority(TicketPriority.Low)
            .AssignedTo(5)
            .Build();

        context.ExistingTicket(ticket);

        context.Agents
            .Setup(a => a.IsEligibleForTicketAsync(
                5,
                ticket.CategoryId,
                false,
                ticket.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var response = await context.EscalateTicket.HandleAsync(
            ticket.Id,
            Request(),
            CancellationToken.None);

        Assert.Equal(5, ticket.AssignedAgentId);
        Assert.Equal(5, response.Escalation.FromAgentId);
        Assert.Equal(5, response.Escalation.ToAgentId);
    }

    [Fact]
    public async Task HandleAsync_WhenCurrentAgentNoLongerEligible_ReassignsToBestAvailable()
    {
        var context = new TicketCommandTestContext();
        var ticket = new TicketBuilder()
            .WithPriority(TicketPriority.Low)
            .AssignedTo(5)
            .Build();

        context.ExistingTicket(ticket);

        context.Agents
            .Setup(a => a.IsEligibleForTicketAsync(
                5,
                ticket.CategoryId,
                false,
                ticket.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var newAgent = new Agent("Ben Osei", "ben@example.com", 8, context.Clock.UtcNow);
        typeof(SupportDesk.Domain.Common.Entity)
            .GetProperty(nameof(SupportDesk.Domain.Common.Entity.Id))!
            .SetValue(newAgent, 6);

        context.Agents
            .Setup(a => a.GetBestAvailableAsync(
                ticket.CategoryId,
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(newAgent);

        var response = await context.EscalateTicket.HandleAsync(
            ticket.Id,
            Request(),
            CancellationToken.None);

        Assert.Equal(6, ticket.AssignedAgentId);
        Assert.Equal(5, response.Escalation.FromAgentId);
        Assert.Equal(6, response.Escalation.ToAgentId);
    }

    [Fact]
    public async Task HandleAsync_RecordsCompleteHistoryRow()
    {
        var context = new TicketCommandTestContext();
        var previousDue = context.Clock.UtcNow.AddHours(12);
        var ticket = new TicketBuilder()
            .WithPriority(TicketPriority.Medium)
            .WithDueAt(previousDue)
            .AssignedTo(1)
            .Build();

        context.ExistingTicket(ticket);

        var response = await context.EscalateTicket.HandleAsync(
            ticket.Id,
            Request("Investigation escalated to tier 2 support.", "Sara Lindqvist"),
            CancellationToken.None);

        var escalation = context.AddedEscalation;
        Assert.NotNull(escalation);
        Assert.Equal(ticket.Id, escalation.TicketId);
        Assert.Equal(TicketPriority.Medium, escalation.FromPriority);
        Assert.Equal(TicketPriority.High, escalation.ToPriority);
        Assert.Equal(1, escalation.FromAgentId);
        Assert.Equal("Investigation escalated to tier 2 support.", escalation.Reason);
        Assert.Equal("Sara Lindqvist", escalation.EscalatedBy);
        Assert.Equal(context.Clock.UtcNow, escalation.EscalatedAtUtc);
    }

    [Fact]
    public async Task HandleAsync_WhenTicketIsCritical_ThrowsConflictException()
    {
        var context = new TicketCommandTestContext();
        var ticket = new TicketBuilder()
            .WithPriority(TicketPriority.Critical)
            .Build();

        context.ExistingTicket(ticket);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => context.EscalateTicket.HandleAsync(
                ticket.Id,
                Request(),
                CancellationToken.None));

        Assert.Contains("Critical", exception.Message);
        context.VerifySaved(Times.Never());
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public async Task HandleAsync_WhenTicketIsResolvedOrClosed_ThrowsConflictException(TicketStatus status)
    {
        var context = new TicketCommandTestContext();
        var ticket = new TicketBuilder()
            .WithStatus(status)
            .Build();

        context.ExistingTicket(ticket);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => context.EscalateTicket.HandleAsync(
                ticket.Id,
                Request(),
                CancellationToken.None));

        Assert.Contains(status.ToString(), exception.Message);
        context.VerifySaved(Times.Never());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("four")] // < 5 chars
    public async Task HandleAsync_WhenReasonIsTooShort_ThrowsArgumentException(string invalidReason)
    {
        var context = new TicketCommandTestContext();
        var ticket = new TicketBuilder().Build();
        context.ExistingTicket(ticket);

        await Assert.ThrowsAsync<ArgumentException>(
            () => context.EscalateTicket.HandleAsync(
                ticket.Id,
                new EscalateTicketRequest(invalidReason, "Alex"),
                CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_WhenReasonIsOver500Chars_ThrowsArgumentException()
    {
        var context = new TicketCommandTestContext();
        var ticket = new TicketBuilder().Build();
        context.ExistingTicket(ticket);

        var longReason = new string('a', 501);

        await Assert.ThrowsAsync<ArgumentException>(
            () => context.EscalateTicket.HandleAsync(
                ticket.Id,
                new EscalateTicketRequest(longReason, "Alex"),
                CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_WhenEscalatedByIsMissing_ThrowsArgumentException(string invalidActor)
    {
        var context = new TicketCommandTestContext();
        var ticket = new TicketBuilder().Build();
        context.ExistingTicket(ticket);

        await Assert.ThrowsAsync<ArgumentException>(
            () => context.EscalateTicket.HandleAsync(
                ticket.Id,
                new EscalateTicketRequest("Valid escalation reason.", invalidActor),
                CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_WhenTicketNotFound_ThrowsNotFoundException()
    {
        var context = new TicketCommandTestContext();
        context.Tickets
            .Setup(t => t.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ticket?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => context.EscalateTicket.HandleAsync(
                999,
                Request(),
                CancellationToken.None));
    }
}
