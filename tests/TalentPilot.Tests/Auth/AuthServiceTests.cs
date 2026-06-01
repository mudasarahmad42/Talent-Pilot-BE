using Microsoft.Extensions.Options;
using TalentPilot.Application.Auth;
using TalentPilot.Common.Time;
using TalentPilot.Domain.Access;
using TalentPilot.Infrastructure.Auth;

namespace TalentPilot.Tests.Auth;

public sealed class AuthServiceTests
{
    private const string DemoPasswordHash = "$2a$10$394j2/GNOR2jpagThC4RWOCkDm2HrM4Mb5nCBrkW3D5OTyQKsH4Nu";

    [Fact]
    public async Task LoginAsync_WithValidPassword_ReturnsTokensAndUserContext()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.LoginAsync(
            new LoginRequest("ai-recruiter@8pkk57.onmicrosoft.com", "demo"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.Value.RefreshToken));
        Assert.Equal(fixture.UserId, result.Value.User.UserId);
        Assert.Equal("ai-recruiter@8pkk57.onmicrosoft.com", result.Value.User.Email);
        Assert.Single(fixture.Repository.RefreshTokens);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong")]
    public async Task LoginAsync_WithMissingOrWrongPassword_Fails(string? password)
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.LoginAsync(
            new LoginRequest("ai-recruiter@8pkk57.onmicrosoft.com", password),
            CancellationToken.None);

        Assert.True(result.Failed);
        Assert.Equal("auth.invalid_credentials", result.Error.Code);
        Assert.Empty(fixture.Repository.RefreshTokens);
    }

    [Fact]
    public async Task LoginAsync_WithDisabledUser_Fails()
    {
        var fixture = CreateFixture("Disabled");

        var result = await fixture.Service.LoginAsync(
            new LoginRequest("ai-recruiter@8pkk57.onmicrosoft.com", "demo"),
            CancellationToken.None);

        Assert.True(result.Failed);
        Assert.Equal("auth.invalid_credentials", result.Error.Code);
    }

    [Fact]
    public async Task RefreshAsync_RotatesRefreshTokenAndRevokesOldToken()
    {
        var fixture = CreateFixture();
        var login = await fixture.Service.LoginAsync(
            new LoginRequest("ai-recruiter@8pkk57.onmicrosoft.com", "demo"),
            CancellationToken.None);

        var result = await fixture.Service.RefreshAsync(
            new RefreshTokenRequest(login.Value.RefreshToken),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotEqual(login.Value.RefreshToken, result.Value.RefreshToken);
        Assert.Equal(2, fixture.Repository.RefreshTokens.Count);
        Assert.NotNull(fixture.Repository.RefreshTokens[0].RevokedAtUtc);
        Assert.Null(fixture.Repository.RefreshTokens[1].RevokedAtUtc);

        var oldTokenResult = await fixture.Service.RefreshAsync(
            new RefreshTokenRequest(login.Value.RefreshToken),
            CancellationToken.None);

        Assert.True(oldTokenResult.Failed);
        Assert.Equal("auth.invalid_refresh_token", oldTokenResult.Error.Code);
    }

    [Fact]
    public async Task LogoutAsync_RevokesRefreshToken()
    {
        var fixture = CreateFixture();
        var login = await fixture.Service.LoginAsync(
            new LoginRequest("ai-recruiter@8pkk57.onmicrosoft.com", "demo"),
            CancellationToken.None);

        var result = await fixture.Service.LogoutAsync(
            new LogoutRequest(login.Value.RefreshToken),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(fixture.Repository.RefreshTokens.Single().RevokedAtUtc);
    }

    private static Fixture CreateFixture(string accountStatus = "Active")
    {
        var clock = new FixedClock(DateTimeOffset.Parse("2026-05-31T08:00:00Z"));
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var userId = Guid.Parse("33333333-3333-3333-3333-333333333304");
        var repository = new FakeIdentityRepository(tenantId, userId, accountStatus);
        var service = new AuthService(
            repository,
            new BCryptPasswordVerifier(),
            new JwtTokenService(
                Options.Create(new JwtOptions
                {
                    Issuer = "TalentPilot",
                    Audience = "TalentPilot.Web",
                    SigningKey = "development-only-change-this-key-before-production-32"
                }),
                clock),
            new SecureTokenGenerator(),
            clock,
            new AuthRuntimeOptions
            {
                AccessTokenMinutes = 60,
                RefreshTokenDays = 7
            });

        return new Fixture(service, repository, userId);
    }

    private sealed record Fixture(AuthService Service, FakeIdentityRepository Repository, Guid UserId);

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; }
    }

    private sealed class FakeIdentityRepository : IIdentityRepository
    {
        private readonly Guid _tenantId;
        private readonly Guid _userId;
        private readonly string _accountStatus;

        public FakeIdentityRepository(Guid tenantId, Guid userId, string accountStatus)
        {
            _tenantId = tenantId;
            _userId = userId;
            _accountStatus = accountStatus;
        }

        public List<RefreshTokenRecord> RefreshTokens { get; } = [];

        public Task<IReadOnlyList<LoginOption>> ListLoginOptionsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<LoginOption>>([]);

        public Task<AuthUserRecord?> FindUserByEmailAsync(string email, CancellationToken cancellationToken)
        {
            if (!email.Equals("ai-recruiter@8pkk57.onmicrosoft.com", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<AuthUserRecord?>(null);
            }

            return Task.FromResult<AuthUserRecord?>(new AuthUserRecord
            {
                UserId = _userId,
                TenantId = _tenantId,
                DisplayName = "Sara Malik",
                Email = "ai-recruiter@8pkk57.onmicrosoft.com",
                AccountStatus = _accountStatus,
                PasswordHash = DemoPasswordHash
            });
        }

        public Task<AuthUserRecord?> FindUserByIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<AuthUserRecord?>(null);

        public Task<CurrentUserData?> GetCurrentUserDataAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
        {
            if (tenantId != _tenantId || userId != _userId)
            {
                return Task.FromResult<CurrentUserData?>(null);
            }

            var data = new CurrentUserData
            {
                UserId = _userId,
                TenantId = _tenantId,
                TenantDisplayName = "TKXEL",
                DisplayName = "Sara Malik",
                Email = "ai-recruiter@8pkk57.onmicrosoft.com",
                PermissionResolutionMode = PermissionResolutionMode.MergeAllAssignedRoles
            };
            data.Roles.Add(new RoleWithPermissions
            {
                RoleId = Guid.Parse("22222222-2222-2222-2222-222222222204"),
                Code = "Recruiter",
                Name = "Recruiter",
                Priority = 30
            });
            data.Roles[0].PermissionIds.Add("job.requests.view");
            data.Roles[0].PermissionIds.Add("workflow.assignments.claim");

            return Task.FromResult<CurrentUserData?>(data);
        }

        public Task TouchLastActiveAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task StoreRefreshTokenAsync(RefreshTokenRecord record, CancellationToken cancellationToken)
        {
            RefreshTokens.Add(record);
            return Task.CompletedTask;
        }

        public Task<RefreshTokenRecord?> FindRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken)
        {
            return Task.FromResult(RefreshTokens.FirstOrDefault(token => token.TokenHash == tokenHash));
        }

        public Task RevokeRefreshTokenAsync(Guid refreshTokenId, DateTimeOffset revokedAtUtc, CancellationToken cancellationToken)
        {
            var index = RefreshTokens.FindIndex(token => token.RefreshTokenId == refreshTokenId);
            if (index >= 0)
            {
                var token = RefreshTokens[index];
                RefreshTokens[index] = new RefreshTokenRecord
                {
                    RefreshTokenId = token.RefreshTokenId,
                    TenantId = token.TenantId,
                    UserId = token.UserId,
                    TokenHash = token.TokenHash,
                    ExpiresAtUtc = token.ExpiresAtUtc,
                    RevokedAtUtc = revokedAtUtc
                };
            }

            return Task.CompletedTask;
        }
    }
}
