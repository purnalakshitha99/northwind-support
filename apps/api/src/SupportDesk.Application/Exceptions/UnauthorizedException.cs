namespace SupportDesk.Application.Exceptions;

/// <summary>
/// Thrown when authentication fails or invalid credentials are provided.
/// </summary>
public sealed class UnauthorizedException : Exception
{
    public const string DefaultMessage = "Invalid email or password.";

    public UnauthorizedException(string message = DefaultMessage)
        : base(message)
    {
    }
}
