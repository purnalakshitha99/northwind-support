using Microsoft.Extensions.Logging;
using Moq;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.UnitTests.Application.Features.Auth.Commands.Login;

public class LoginCommandHandlerTests
{
    private readonly Mock<IAgentRepository> _agentRepo = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<ILogger<LoginCommandHandler>> _logger = new();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _handler = new LoginCommandHandler(
            _agentRepo.Object,
            _passwordHasher.Object,
            _tokenService.Object,
            _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_ReturnsTokenAndAgent_WhenCredentialsAreValid()
    {
        // Arrange
        var agent = new Agent("Alex Turner", "alex.turner@northwind-support.example", 10, DateTime.UtcNow, "hashed_secret");
        typeof(Agent).BaseType!.GetProperty("Id")!.SetValue(agent, 1);

        _agentRepo.Setup(r => r.GetByEmailAsync("alex.turner@northwind-support.example", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _passwordHasher.Setup(h => h.VerifyPassword("Password123!", "hashed_secret"))
            .Returns(true);

        var expires = DateTime.UtcNow.AddHours(8);
        _tokenService.Setup(t => t.GenerateToken(agent))
            .Returns(("jwt.valid.token", expires));

        // Act
        var result = await _handler.HandleAsync(
            new LoginRequest("alex.turner@northwind-support.example", "Password123!"),
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("jwt.valid.token", result.Token);
        Assert.Equal(expires, result.ExpiresAtUtc);
        Assert.Equal(1, result.Agent.Id);
        Assert.Equal("Alex Turner", result.Agent.FullName);
        Assert.Equal("alex.turner@northwind-support.example", result.Agent.Email);
    }

    [Fact]
    public async Task HandleAsync_ThrowsUnauthorizedException_WhenAgentNotFound()
    {
        _agentRepo.Setup(r => r.GetByEmailAsync("nonexistent@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _handler.HandleAsync(new LoginRequest("nonexistent@example.com", "Password123!"), CancellationToken.None));

        Assert.Equal(UnauthorizedException.DefaultMessage, exception.Message);
    }

    [Fact]
    public async Task HandleAsync_ThrowsUnauthorizedException_WhenPasswordIsIncorrect()
    {
        var agent = new Agent("Alex Turner", "alex.turner@northwind-support.example", 10, DateTime.UtcNow, "hashed_secret");

        _agentRepo.Setup(r => r.GetByEmailAsync("alex.turner@northwind-support.example", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _passwordHasher.Setup(h => h.VerifyPassword("WrongPassword!", "hashed_secret"))
            .Returns(false);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _handler.HandleAsync(new LoginRequest("alex.turner@northwind-support.example", "WrongPassword!"), CancellationToken.None));

        Assert.Equal(UnauthorizedException.DefaultMessage, exception.Message);
    }

    [Fact]
    public async Task HandleAsync_ThrowsUnauthorizedException_WhenAgentIsInactive()
    {
        var inactiveAgent = new Agent("Yuki Tanaka", "yuki.tanaka@northwind-support.example", 8, DateTime.UtcNow, "hashed_secret");
        inactiveAgent.Deactivate();

        _agentRepo.Setup(r => r.GetByEmailAsync("yuki.tanaka@northwind-support.example", It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveAgent);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _handler.HandleAsync(new LoginRequest("yuki.tanaka@northwind-support.example", "Password123!"), CancellationToken.None));

        Assert.Equal(UnauthorizedException.DefaultMessage, exception.Message);
    }

    [Theory]
    [InlineData("", "Password123!")]
    [InlineData("   ", "Password123!")]
    [InlineData("alex@example.com", "")]
    [InlineData("alex@example.com", "   ")]
    public async Task HandleAsync_ThrowsUnauthorizedException_WhenInputsAreEmptyOrWhitespace(string email, string password)
    {
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _handler.HandleAsync(new LoginRequest(email, password), CancellationToken.None));

        Assert.Equal(UnauthorizedException.DefaultMessage, exception.Message);
    }
}
