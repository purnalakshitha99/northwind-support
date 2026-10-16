using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Application.Abstractions;

/// <summary>
/// Issues signed authentication tokens carrying agent identity claims.
/// </summary>
public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) GenerateToken(Agent agent);
}
