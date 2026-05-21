using TalentPilot.Application.Auth;

namespace TalentPilot.Infrastructure.Auth;

public sealed class BCryptPasswordVerifier : IPasswordVerifier
{
    public bool Verify(string password, string passwordHash)
    {
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }
}
