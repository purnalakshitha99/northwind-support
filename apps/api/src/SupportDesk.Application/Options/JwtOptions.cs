namespace SupportDesk.Application.Options;

/// <summary>
/// Configuration for issuing and validating JWT bearer authentication tokens.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Authentication:Jwt";

    /// <summary>HMAC-SHA256 symmetric secret key. Must be at least 32 characters.</summary>
    public string SecretKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "SupportDesk";

    public string Audience { get; set; } = "SupportDeskWeb";

    /// <summary>Token lifetime in minutes. Defaults to 480 (8-hour support shift).</summary>
    public int ExpiryMinutes { get; set; } = 480;
}
