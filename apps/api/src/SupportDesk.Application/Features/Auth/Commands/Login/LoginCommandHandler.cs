using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Commands.Login;

public sealed class LoginCommandHandler(
    IAgentRepository agents,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    ILogger<LoginCommandHandler> logger)
{
    public async Task<LoginResponse> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new UnauthorizedException();
        }

        var agent = await agents.GetByEmailAsync(request.Email.Trim(), ct);

        // AU-4: Wrong email, wrong password, or inactive agent all return the identical 401 error.
        if (agent is null || !agent.IsActive || string.IsNullOrEmpty(agent.PasswordHash))
        {
            logger.LogWarning("Authentication rejected for '{Email}': agent not found, inactive, or unconfigured.", request.Email);
            throw new UnauthorizedException();
        }

        if (!passwordHasher.VerifyPassword(request.Password, agent.PasswordHash))
        {
            logger.LogWarning("Authentication rejected for '{Email}': invalid password.", request.Email);
            throw new UnauthorizedException();
        }

        var (token, expiresAtUtc) = tokenService.GenerateToken(agent);

        logger.LogInformation("Agent {AgentId} ({Email}) authenticated successfully.", agent.Id, agent.Email);

        return new LoginResponse(
            token,
            expiresAtUtc,
            new CurrentUserDto(agent.Id, agent.FullName, agent.Email));
    }
}
