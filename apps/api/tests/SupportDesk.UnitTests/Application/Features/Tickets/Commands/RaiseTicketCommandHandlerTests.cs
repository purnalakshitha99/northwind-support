using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class RaiseTicketCommandHandlerTests
{
    private static CreateTicketRequest Request(TicketPriority? priority = null) =>
        new(
            "Cannot log in to the portal",
            "Happens for every user since this morning.",
            1,
            3,
            priority);

    [Fact]
    public async Task HandleAsync_StampsReferenceStatusAndTimestamps()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(
            Request(TicketPriority.High),
            CancellationToken.None);

        var created = context.AddedTicket;
        Assert.NotNull(created);
        Assert.Equal("TCK-0041", created.Reference);
        Assert.Equal(TicketStatus.New, created.Status);
        Assert.Equal(TicketPriority.High, created.Priority);
        Assert.Equal(FixedClock.DefaultNow, created.CreatedAtUtc);
        Assert.Equal(FixedClock.DefaultNow, created.UpdatedAtUtc);

        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WithoutRequestedPriority_FallsBackToMedium()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(
            Request(),
            CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(TicketPriority.Medium, context.AddedTicket.Priority);
    }

    [Fact]
    public async Task HandleAsync_WhenCustomerDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();

        context.Customers
            .Setup(c => c.GetByIdAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(
                Request(),
                CancellationToken.None));

        Assert.Contains("Customer", exception.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenCategoryForcesCriticalPriority_SetsCriticalEvenIfRequestedLow()
    {
        var context = new TicketCommandTestContext();

        var outageCategory = new Category(
            "Outage",
            requiresSpecialist: true,
            forcesCriticalPriority: true);

        context.Categories
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(outageCategory);

        await context.RaiseTicket.HandleAsync(
            Request(TicketPriority.Low),
            CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(TicketPriority.Critical, context.AddedTicket.Priority);
    }

    [Fact]
    public async Task HandleAsync_CalculatesSlaWindow_ForStandardCustomer()
    {
        var context = new TicketCommandTestContext();

        var standardCustomer = new Customer(
            "Fabrikam",
            "info@fabrikam.example",
            null,
            CustomerTier.Standard,
            context.Clock.UtcNow);

        context.Customers
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(standardCustomer);

        // Medium base = 24h for Standard
        await context.RaiseTicket.HandleAsync(
            Request(TicketPriority.Medium),
            CancellationToken.None);

        var created = context.AddedTicket;
        Assert.NotNull(created);
        Assert.Equal(context.Clock.UtcNow.AddHours(24), created.DueAtUtc);
        Assert.Equal(context.Clock.UtcNow, created.SlaWindowStartAtUtc);
    }

    [Fact]
    public async Task HandleAsync_CalculatesSlaWindow_ForPremiumCustomer_HalvesWindow()
    {
        var context = new TicketCommandTestContext();

        var premiumCustomer = new Customer(
            "Contoso",
            "info@contoso.example",
            null,
            CustomerTier.Premium,
            context.Clock.UtcNow);

        context.Customers
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(premiumCustomer);

        // Medium base = 24h, Premium 50% = 12h
        await context.RaiseTicket.HandleAsync(
            Request(TicketPriority.Medium),
            CancellationToken.None);

        var created = context.AddedTicket;
        Assert.NotNull(created);
        Assert.Equal(context.Clock.UtcNow.AddHours(12), created.DueAtUtc);
        Assert.Equal(context.Clock.UtcNow, created.SlaWindowStartAtUtc);
    }

    [Fact]
    public async Task HandleAsync_WhenCategoryRequiresSpecialist_QueriesSpecialists()
    {
        var context = new TicketCommandTestContext();

        var billingCategory = new Category(
            "Billing",
            requiresSpecialist: true,
            forcesCriticalPriority: false);

        typeof(Category).GetProperty(nameof(Category.Id))!.SetValue(billingCategory, 2);

        context.Categories
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(billingCategory);

        await context.RaiseTicket.HandleAsync(
            Request(TicketPriority.Medium),
            CancellationToken.None);

        context.Agents.Verify(
            a => a.GetBestAvailableAsync(2, true, It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WhenNoEligibleAgent_LeavesTicketUnassignedWithReason()
    {
        var context = new TicketCommandTestContext();

        context.Agents
            .Setup(a => a.GetBestAvailableAsync(
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);

        var result = await context.RaiseTicket.HandleAsync(
            Request(TicketPriority.Medium),
            CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Null(context.AddedTicket.AssignedAgentId);
        Assert.Contains("unassigned", result.TriageReason, StringComparison.OrdinalIgnoreCase);
    }
}
