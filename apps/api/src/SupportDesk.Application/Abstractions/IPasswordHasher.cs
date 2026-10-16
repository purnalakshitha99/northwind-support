namespace SupportDesk.Application.Abstractions;

/// <summary>
/// Provides secure hashing and verification of agent passwords using a vetted cryptographic mechanism.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Hashes and salts a plaintext password.</summary>
    string HashPassword(string password);

    /// <summary>Verifies whether the provided plaintext password matches the stored hash and salt.</summary>
    bool VerifyPassword(string password, string passwordHash);
}
