namespace SupportDesk.Application.Contracts.Auth;

/// <summary>Request body for POST /api/auth/login.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Public agent profile returned to the authenticated client.</summary>
public sealed record CurrentUserDto(int Id, string FullName, string Email);

/// <summary>Successful authentication response containing JWT token and agent details.</summary>
public sealed record LoginResponse(string Token, DateTime ExpiresAtUtc, CurrentUserDto Agent);
