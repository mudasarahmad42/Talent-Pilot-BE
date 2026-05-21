using System.Security.Cryptography;
using TalentPilot.Application.Auth;

namespace TalentPilot.Infrastructure.Auth;

public sealed class SecureTokenGenerator : ITokenGenerator
{
    public string CreateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    public string HashToken(string token)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
