
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;
using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public sealed class TicketQueriesTests
{
    [Fact]
    public async Task GetPagedAsync_FiltersByExactPriority()
    {
        // Arrange: create a real in-memory SQLite database.
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var customer = new Customer(
            "Test Customer",
            "customer@example.com",
            null,
            Enum.GetValues<CustomerTier>()[0],
            FixedClock.DefaultNow);

        var category = new Category("General", false, false);

        db.Set<Customer>().Add(customer);
        db.Set<Category>().Add(category);
        await db.SaveChangesAsync();

        db.Set<Ticket>().AddRange(
            Ticket.Raise(
                "TCK-1001",
                "High priority ticket",
                "Test description for high priority.",
                customer.Id,
                category.Id,
                TicketPriority.High,
                FixedClock.DefaultNow),
            Ticket.Raise(
                "TCK-1002",
                "Medium priority ticket",
                "Test description for medium priority.",
                customer.Id,
                category.Id,
                TicketPriority.Medium,
                FixedClock.DefaultNow));

        await db.SaveChangesAsync();

        var queries = new TicketQueries(db, new FixedClock());

        var query = new TicketQuery
        {
            Priority = TicketPriority.High,
            Page = 1,
            PageSize = 20
        };

        // Act
        var result = await queries.GetPagedAsync(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Items);
        Assert.All(
            result.Items,
            ticket => Assert.Equal(TicketPriority.High, ticket.Priority));
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_CombinesStatusAndPriorityFilters()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var customer = new Customer(
            "Combined Filter Customer",
            "combined@example.com",
            null,
            Enum.GetValues<CustomerTier>()[0],
            FixedClock.DefaultNow);

        var category = new Category("Combined General", false, false);

        db.Set<Customer>().Add(customer);
        db.Set<Category>().Add(category);
        await db.SaveChangesAsync();

        db.Set<Ticket>().AddRange(
            Ticket.Raise(
                "TCK-2001", "Matching ticket", "Description for matching ticket.",
                customer.Id, category.Id, TicketPriority.High, FixedClock.DefaultNow),

            Ticket.Raise(
                "TCK-2002", "Wrong priority", "Description for wrong priority.",
                customer.Id, category.Id, TicketPriority.Medium, FixedClock.DefaultNow));

        await db.SaveChangesAsync();

        var matchingTicket = await db.Set<Ticket>()
            .SingleAsync(t => t.Reference == "TCK-2001");

        matchingTicket.ChangeStatus(TicketStatus.InProgress, FixedClock.DefaultNow);
        await db.SaveChangesAsync();

        var queries = new TicketQueries(db, new FixedClock());

        var result = await queries.GetPagedAsync(
            new TicketQuery
            {
                Status = TicketStatus.InProgress,
                Priority = TicketPriority.High,
                Page = 1,
                PageSize = 20
            },
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("TCK-2001", result.Items[0].Reference);
        Assert.Equal(TicketStatus.InProgress, result.Items[0].Status);
        Assert.Equal(TicketPriority.High, result.Items[0].Priority);
        Assert.Equal(1, result.TotalCount);
    }


    [Fact]
    public async Task GetPagedAsync_SearchesByCustomerName()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var customer = new Customer(
            "Acme Support Customer",
            "acme@example.com",
            null,
            Enum.GetValues<CustomerTier>()[0],
            FixedClock.DefaultNow);

        var category = new Category("Customer Search General", false, false);

        db.Set<Customer>().Add(customer);
        db.Set<Category>().Add(category);
        await db.SaveChangesAsync();

        db.Set<Ticket>().Add(
            Ticket.Raise(
                "TCK-3001",
                "Cannot log in",
                "Login is not working.",
                customer.Id,
                category.Id,
                TicketPriority.Medium,
                FixedClock.DefaultNow));

        await db.SaveChangesAsync();

        var queries = new TicketQueries(db, new FixedClock());

        var result = await queries.GetPagedAsync(
            new TicketQuery
            {
                Search = "  Acme Support  ",
                Page = 1,
                PageSize = 20
            },
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("TCK-3001", result.Items[0].Reference);
        Assert.Equal("Acme Support Customer", result.Items[0].Customer.Name);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersByAssignedAgentAndUnassignedOnly()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var customer = new Customer(
            "Assignment Test Customer",
            "assignment@example.com",
            null,
            Enum.GetValues<CustomerTier>()[0],
            FixedClock.DefaultNow);

        var category = new Category("Assignment General", false, false);

        var agent = new Agent(
            "Test Agent",
            "agent@example.com",
            10,
            FixedClock.DefaultNow);

        db.Set<Customer>().Add(customer);
        db.Set<Category>().Add(category);
        db.Set<Agent>().Add(agent);

        await db.SaveChangesAsync();

        var assignedTicket = Ticket.Raise(
            "TCK-4001",
            "Assigned ticket",
            "This ticket is assigned.",
            customer.Id,
            category.Id,
            TicketPriority.High,
            FixedClock.DefaultNow);

        var unassignedTicket = Ticket.Raise(
            "TCK-4002",
            "Unassigned ticket",
            "This ticket is not assigned.",
            customer.Id,
            category.Id,
            TicketPriority.Medium,
            FixedClock.DefaultNow);

        assignedTicket.AssignTo(agent.Id, FixedClock.DefaultNow);

        db.Set<Ticket>().AddRange(assignedTicket, unassignedTicket);
        await db.SaveChangesAsync();

        var queries = new TicketQueries(db, new FixedClock());

        // Act 1: Filter by assigned agent.
        var assignedResult = await queries.GetPagedAsync(
            new TicketQuery
            {
                AssignedAgentId = agent.Id,
                Page = 1,
                PageSize = 20
            },
            CancellationToken.None);

        // Assert 1
        Assert.Single(assignedResult.Items);
        Assert.Equal("TCK-4001", assignedResult.Items[0].Reference);
        Assert.Equal(1, assignedResult.TotalCount);

        // Act 2: Filter unassigned tickets.
        var unassignedResult = await queries.GetPagedAsync(
            new TicketQuery
            {
                UnassignedOnly = true,
                Page = 1,
                PageSize = 20
            },
            CancellationToken.None);

        // Assert 2
        Assert.Single(unassignedResult.Items);
        Assert.Equal("TCK-4002", unassignedResult.Items[0].Reference);
        Assert.Equal(1, unassignedResult.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersByCategoryAndCustomer()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var customer1 = new Customer(
            "Customer One",
            "customer1@example.com",
            null,
            Enum.GetValues<CustomerTier>()[0],
            FixedClock.DefaultNow);

        var customer2 = new Customer(
            "Customer Two",
            "customer2@example.com",
            null,
            Enum.GetValues<CustomerTier>()[0],
            FixedClock.DefaultNow);

        var category1 = new Category("Category One", false, false);
        var category2 = new Category("Category Two", false, false);

        db.Set<Customer>().AddRange(customer1, customer2);
        db.Set<Category>().AddRange(category1, category2);

        await db.SaveChangesAsync();

        db.Set<Ticket>().AddRange(
            Ticket.Raise(
                "TCK-5001", "First ticket", "First description",
                customer1.Id, category1.Id, TicketPriority.High, FixedClock.DefaultNow),

            Ticket.Raise(
                "TCK-5002", "Second ticket", "Second description",
                customer2.Id, category1.Id, TicketPriority.High, FixedClock.DefaultNow),

            Ticket.Raise(
                "TCK-5003", "Third ticket", "Third description",
                customer1.Id, category2.Id, TicketPriority.High, FixedClock.DefaultNow));

        await db.SaveChangesAsync();

        var queries = new TicketQueries(db, new FixedClock());

        var result = await queries.GetPagedAsync(
            new TicketQuery
            {
                CategoryId = category1.Id,
                CustomerId = customer1.Id,
                Page = 1,
                PageSize = 20
            },
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("TCK-5001", result.Items[0].Reference);
        Assert.Equal(1, result.TotalCount);
    }


    [Fact]
    public async Task GetPagedAsync_ReturnsCorrectPageAndTotalCount()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var customer = new Customer(
            "Pagination Customer",
            "pagination@example.com",
            null,
            Enum.GetValues<CustomerTier>()[0],
            FixedClock.DefaultNow);

        var category = new Category("Pagination General", false, false);

        db.Set<Customer>().Add(customer);
        db.Set<Category>().Add(category);
        await db.SaveChangesAsync();

        db.Set<Ticket>().AddRange(
            Ticket.Raise(
                "TCK-6001", "Ticket One", "First description",
                customer.Id, category.Id, TicketPriority.High, FixedClock.DefaultNow),
            Ticket.Raise(
                "TCK-6002", "Ticket Two", "Second description",
                customer.Id, category.Id, TicketPriority.Medium, FixedClock.DefaultNow),
            Ticket.Raise(
                "TCK-6003", "Ticket Three", "Third description",
                customer.Id, category.Id, TicketPriority.Low, FixedClock.DefaultNow));

        await db.SaveChangesAsync();

        var queries = new TicketQueries(db, new FixedClock());

        var firstPage = await queries.GetPagedAsync(
            new TicketQuery
            {
                Page = 1,
                PageSize = 2,
                SortBy = "createdAtUtc",
                SortDirection = "asc"
            },
            CancellationToken.None);

        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(1, firstPage.Page);
        Assert.Equal(2, firstPage.PageSize);

        var secondPage = await queries.GetPagedAsync(
            new TicketQuery
            {
                Page = 2,
                PageSize = 2,
                SortBy = "createdAtUtc",
                SortDirection = "asc"
            },
            CancellationToken.None);

        Assert.Single(secondPage.Items);
        Assert.Equal(3, secondPage.TotalCount);
        Assert.Equal(2, secondPage.Page);
        Assert.Equal("TCK-6001", secondPage.Items[0].Reference);
    }
}
