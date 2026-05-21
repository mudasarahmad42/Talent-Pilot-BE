using TalentPilot.Common.Results;

namespace TalentPilot.Application.Auth;

public interface IAuthService
{
    Task<Result<IReadOnlyList<LoginOption>>> ListLoginOptionsAsync(CancellationToken cancellationToken);

    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<Result<AuthResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken);

    Task<Result<CurrentUserContext>> GetCurrentUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    Task<Result> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken);
}

public interface IIdentityRepository
{
    Task<IReadOnlyList<LoginOption>> ListLoginOptionsAsync(CancellationToken cancellationToken);

    Task<AuthUserRecord?> FindUserByEmailAsync(string email, CancellationToken cancellationToken);

    Task<AuthUserRecord?> FindUserByIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    Task<CurrentUserData?> GetCurrentUserDataAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    Task TouchLastActiveAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    Task StoreRefreshTokenAsync(RefreshTokenRecord record, CancellationToken cancellationToken);

    Task<RefreshTokenRecord?> FindRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken);

    Task RevokeRefreshTokenAsync(Guid refreshTokenId, DateTimeOffset revokedAtUtc, CancellationToken cancellationToken);
}

public interface IPasswordVerifier
{
    bool Verify(string password, string passwordHash);
}

public interface IJwtTokenService
{
    JwtTokenResult CreateAccessToken(CurrentUserContext user, TimeSpan lifetime);
}

public interface ITokenGenerator
{
    string CreateRefreshToken();

    string HashToken(string token);
}
