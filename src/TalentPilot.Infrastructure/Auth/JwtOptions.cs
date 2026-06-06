namespace TalentPilot.Infrastructure.Auth;

public sealed class JwtOptions
{
    public string Issuer { get; init; } = "TalentPilot";

    public string Audience { get; init; } = "TalentPilot.Web";

    public string SigningKey { get; init; } = string.Empty;
}
