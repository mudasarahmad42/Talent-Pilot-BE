using TalentPilot.Common.Results;
using TalentPilot.Common.Time;
using TalentPilot.Domain.Access;
using System.Net.Mail;

namespace TalentPilot.Application.Auth;

public sealed class AuthService : IAuthService
{
    private readonly IIdentityRepository _identityRepository;
    private readonly IPasswordVerifier _passwordVerifier;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ITokenGenerator _tokenGenerator;
    private readonly IClock _clock;
    private readonly AuthRuntimeOptions _options;
    private readonly EffectivePermissionResolver _permissionResolver = new();

    public AuthService(
        IIdentityRepository identityRepository,
        IPasswordVerifier passwordVerifier,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        ITokenGenerator tokenGenerator,
        IClock clock,
        AuthRuntimeOptions options)
    {
        _identityRepository = identityRepository;
        _passwordVerifier = passwordVerifier;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _tokenGenerator = tokenGenerator;
        _clock = clock;
        _options = options;
    }

    public async Task<Result<IReadOnlyList<LoginOption>>> ListLoginOptionsAsync(CancellationToken cancellationToken)
    {
        var options = await _identityRepository.ListLoginOptionsAsync(cancellationToken);
        return Result<IReadOnlyList<LoginOption>>.Success(options);
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Result<AuthResponse>.Failure("auth.email_required", "Email is required.");
        }

        var user = await _identityRepository.FindUserByEmailAsync(request.Email.Trim(), cancellationToken);
        if (user is null || string.Equals(user.AccountStatus, "Disabled", StringComparison.OrdinalIgnoreCase))
        {
            return Result<AuthResponse>.Failure("auth.invalid_credentials", "Invalid credentials.");
        }

        var passwordAccepted =
            !string.IsNullOrWhiteSpace(request.Password) &&
            !string.IsNullOrWhiteSpace(user.PasswordHash) &&
            _passwordVerifier.Verify(request.Password, user.PasswordHash);

        if (!passwordAccepted)
        {
            return Result<AuthResponse>.Failure("auth.invalid_credentials", "Invalid credentials.");
        }

        var contextResult = await BuildCurrentUserAsync(user.TenantId, user.UserId, cancellationToken);
        if (contextResult.Failed)
        {
            return Result<AuthResponse>.Failure(contextResult.Error.Code, contextResult.Error.Message);
        }

        await _identityRepository.TouchLastActiveAsync(user.TenantId, user.UserId, cancellationToken);
        var response = await CreateAuthResponseAsync(contextResult.Value, cancellationToken);
        return Result<AuthResponse>.Success(response);
    }

    public async Task<Result<AuthResponse>> RegisterCandidateAsync(
        CandidateSignupRequest request,
        CancellationToken cancellationToken)
    {
        var displayName = request.DisplayName?.Trim() ?? string.Empty;
        var email = request.Email?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;

        if (displayName.Length < 2)
        {
            return Result<AuthResponse>.Failure(
                "auth.candidate_signup_name_invalid",
                "Enter your full name to create a candidate account.");
        }

        if (!IsValidEmail(email))
        {
            return Result<AuthResponse>.Failure(
                "auth.candidate_signup_email_invalid",
                "Enter a valid email address.");
        }

        if (password.Length < 8)
        {
            return Result<AuthResponse>.Failure(
                "auth.candidate_signup_password_invalid",
                "Password must be at least 8 characters.");
        }

        var registration = await _identityRepository.RegisterCandidateAsync(
            new CandidateSignupRegistrationInput(
                string.IsNullOrWhiteSpace(request.TenantSlug) ? null : request.TenantSlug.Trim(),
                request.JobPostId,
                displayName,
                email,
                _passwordHasher.Hash(password)),
            cancellationToken);

        if (registration.Status != CandidateSignupStatus.Created || registration.User is null)
        {
            var mapped = MapCandidateSignupFailure(registration.Status);
            return Result<AuthResponse>.Failure(mapped.Code, mapped.Message);
        }

        var contextResult = await BuildCurrentUserAsync(
            registration.User.TenantId,
            registration.User.UserId,
            cancellationToken);
        if (contextResult.Failed)
        {
            return Result<AuthResponse>.Failure(contextResult.Error.Code, contextResult.Error.Message);
        }

        await _identityRepository.TouchLastActiveAsync(
            registration.User.TenantId,
            registration.User.UserId,
            cancellationToken);
        var response = await CreateAuthResponseAsync(contextResult.Value, cancellationToken);
        return Result<AuthResponse>.Success(response);
    }

    public async Task<Result<AuthResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Result<AuthResponse>.Failure("auth.refresh_token_required", "Refresh token is required.");
        }

        var tokenHash = _tokenGenerator.HashToken(request.RefreshToken);
        var token = await _identityRepository.FindRefreshTokenAsync(tokenHash, cancellationToken);
        if (token is null || token.RevokedAtUtc is not null || token.ExpiresAtUtc <= _clock.UtcNow)
        {
            return Result<AuthResponse>.Failure("auth.invalid_refresh_token", "Refresh token is invalid or expired.");
        }

        var contextResult = await BuildCurrentUserAsync(token.TenantId, token.UserId, cancellationToken);
        if (contextResult.Failed)
        {
            return Result<AuthResponse>.Failure(contextResult.Error.Code, contextResult.Error.Message);
        }

        await _identityRepository.RevokeRefreshTokenAsync(token.RefreshTokenId, _clock.UtcNow, cancellationToken);
        var response = await CreateAuthResponseAsync(contextResult.Value, cancellationToken);
        return Result<AuthResponse>.Success(response);
    }

    public Task<Result<CurrentUserContext>> GetCurrentUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return BuildCurrentUserAsync(tenantId, userId, cancellationToken);
    }

    public async Task<Result> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Result.Failure("auth.refresh_token_required", "Refresh token is required.");
        }

        var tokenHash = _tokenGenerator.HashToken(request.RefreshToken);
        var token = await _identityRepository.FindRefreshTokenAsync(tokenHash, cancellationToken);
        if (token is not null && token.RevokedAtUtc is null)
        {
            await _identityRepository.RevokeRefreshTokenAsync(token.RefreshTokenId, _clock.UtcNow, cancellationToken);
        }

        return Result.Success();
    }

    private async Task<Result<CurrentUserContext>> BuildCurrentUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var data = await _identityRepository.GetCurrentUserDataAsync(tenantId, userId, cancellationToken);
        if (data is null)
        {
            return Result<CurrentUserContext>.Failure("auth.user_not_found", "Current user could not be resolved.");
        }

        var orderedRoles = data.Roles
            .OrderBy(role => role.Priority)
            .ThenBy(role => role.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var roleGrants = orderedRoles
            .Select(role => new RoleGrant(role.RoleId, role.Name, role.Priority, role.PermissionIds))
            .ToArray();

        var permissions = _permissionResolver.Resolve(roleGrants, data.PermissionResolutionMode);
        var displayRole = _permissionResolver.ResolveDisplayRole(roleGrants);
        var context = new CurrentUserContext(
            data.UserId,
            data.TenantId,
            data.TenantDisplayName,
            data.DisplayName,
            data.Email,
            displayRole?.Name ?? "No assigned role",
            orderedRoles
                .Select(role => new CurrentUserRole(role.RoleId, role.Code, role.Name, role.Priority))
                .ToArray(),
            permissions,
            data.Groups
                .OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            RouteAccessMapper.MapRoutes(permissions));

        return Result<CurrentUserContext>.Success(context);
    }

    private async Task<AuthResponse> CreateAuthResponseAsync(CurrentUserContext context, CancellationToken cancellationToken)
    {
        var accessToken = _jwtTokenService.CreateAccessToken(context, TimeSpan.FromMinutes(_options.AccessTokenMinutes));
        var refreshToken = _tokenGenerator.CreateRefreshToken();

        await _identityRepository.StoreRefreshTokenAsync(
            new RefreshTokenRecord
            {
                RefreshTokenId = Guid.NewGuid(),
                TenantId = context.TenantId,
                UserId = context.UserId,
                TokenHash = _tokenGenerator.HashToken(refreshToken),
                ExpiresAtUtc = _clock.UtcNow.AddDays(_options.RefreshTokenDays)
            },
            cancellationToken);

        return new AuthResponse(accessToken.AccessToken, refreshToken, accessToken.ExpiresAtUtc, context);
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        try
        {
            var address = new MailAddress(email);
            return string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static (string Code, string Message) MapCandidateSignupFailure(CandidateSignupStatus status)
    {
        return status switch
        {
            CandidateSignupStatus.TenantRequired => (
                "auth.candidate_signup_tenant_required",
                "Choose a company career portal before creating a candidate account."),
            CandidateSignupStatus.JobNotFound => (
                "auth.candidate_signup_job_not_found",
                "The selected job is no longer available for public applications."),
            CandidateSignupStatus.PublicJobsDisabled => (
                "auth.candidate_signup_public_jobs_disabled",
                "This company is not accepting public candidate account signups right now."),
            CandidateSignupStatus.CandidateRoleMissing => (
                "auth.candidate_signup_role_missing",
                "Candidate signup is not configured for this tenant."),
            CandidateSignupStatus.EmailExists => (
                "auth.candidate_signup_email_exists",
                "An account already exists for this email. Sign in to continue."),
            _ => (
                "auth.candidate_signup_failed",
                "Candidate account could not be created.")
        };
    }
}
