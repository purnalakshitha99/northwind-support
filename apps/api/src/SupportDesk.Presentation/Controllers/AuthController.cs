using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    /// <summary>
    /// Authenticates an agent using their email and password, returning a JWT token and user info.
    /// Wrong email and wrong password yield an identical generic 401 response.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        [FromServices] LoginCommandHandler handler,
        CancellationToken ct)
    {
        var response = await handler.HandleAsync(request, ct);
        return Ok(response);
    }

    /// <summary>
    /// Returns the currently authenticated agent's profile from the bearer token.
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserDto>> GetCurrentUser(
        [FromServices] IAgentRepository agents,
        CancellationToken ct)
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(idClaim) || !int.TryParse(idClaim, out var agentId))
        {
            return Unauthorized();
        }

        var agent = await agents.GetByIdAsync(agentId, ct);
        if (agent is null || !agent.IsActive)
        {
            return Unauthorized();
        }

        return Ok(new CurrentUserDto(agent.Id, agent.FullName, agent.Email));
    }
}
