using SupportDesk.Infrastructure.Services;

namespace SupportDesk.UnitTests.Infrastructure.Services;

public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void HashPassword_ThrowsArgumentException_WhenPasswordIsNullOrEmpty()
    {
        Assert.Throws<ArgumentException>(() => _hasher.HashPassword(""));
        Assert.Throws<ArgumentNullException>(() => _hasher.HashPassword(null!));
        Assert.Throws<ArgumentException>(() => _hasher.HashPassword("   "));
    }

    [Fact]
    public void HashPassword_GeneratesValidSaltedHashWithDelimiters()
    {
        var hash = _hasher.HashPassword("SecurePassword123!");

        var parts = hash.Split(':');
        Assert.Equal(3, parts.Length);
        Assert.True(int.TryParse(parts[0], out var iterations));
        Assert.True(iterations >= 100_000);

        var salt = Convert.FromBase64String(parts[1]);
        Assert.Equal(16, salt.Length); // 128-bit salt

        var key = Convert.FromBase64String(parts[2]);
        Assert.Equal(32, key.Length); // 256-bit subkey
    }

    [Fact]
    public void HashPassword_ProducesDifferentHashesForSamePassword_DueToUniqueSalts()
    {
        var hash1 = _hasher.HashPassword("Password123!");
        var hash2 = _hasher.HashPassword("Password123!");

        Assert.NotEqual(hash1, hash2);
        Assert.True(_hasher.VerifyPassword("Password123!", hash1));
        Assert.True(_hasher.VerifyPassword("Password123!", hash2));
    }

    [Fact]
    public void VerifyPassword_ReturnsTrue_ForCorrectPassword()
    {
        var hash = _hasher.HashPassword("CorrectHorseBatteryStaple");

        var result = _hasher.VerifyPassword("CorrectHorseBatteryStaple", hash);

        Assert.True(result);
    }

    [Fact]
    public void VerifyPassword_ReturnsFalse_ForIncorrectPassword()
    {
        var hash = _hasher.HashPassword("Password123!");

        var result = _hasher.VerifyPassword("WrongPassword123!", hash);

        Assert.False(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-valid-hash")]
    [InlineData("100000:invalidbase64:invalidbase64")]
    [InlineData("invalid:AAAA:BBBB")]
    public void VerifyPassword_ReturnsFalse_ForMalformedHash(string malformedHash)
    {
        var result = _hasher.VerifyPassword("Password123!", malformedHash);

        Assert.False(result);
    }
}
