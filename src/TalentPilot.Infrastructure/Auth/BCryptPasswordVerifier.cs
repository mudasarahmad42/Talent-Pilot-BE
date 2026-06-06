using TalentPilot.Application.Auth;

namespace TalentPilot.Infrastructure.Auth;

public sealed class BCryptPasswordVerifier : IPasswordVerifier, IPasswordHasher
{
    public string Hash(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public bool Verify(string password, string passwordHash)
    {
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }
}
