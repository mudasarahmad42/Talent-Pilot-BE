using Dapper;
using TalentPilot.Application.Calendar;

namespace TalentPilot.Infrastructure.Persistence.Repositories;

public sealed class DapperGoogleCalendarConnectionRepository : IGoogleCalendarConnectionRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public DapperGoogleCalendarConnectionRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task StoreOAuthStateAsync(GoogleCalendarOAuthState state, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.GoogleCalendarOAuthStates
            (
                StateHash,
                TenantId,
                UserId,
                UserEmail,
                ExpiresAtUtc,
                CreatedAtUtc,
                ConsumedAtUtc
            )
            VALUES
            (
                @StateHash,
                @TenantId,
                @UserId,
                @UserEmail,
                @ExpiresAtUtc,
                @CreatedAtUtc,
                @ConsumedAtUtc
            );
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                state.StateHash,
                state.TenantId,
                state.UserId,
                state.UserEmail,
                ExpiresAtUtc = state.ExpiresAtUtc.UtcDateTime,
                CreatedAtUtc = state.CreatedAtUtc.UtcDateTime,
                ConsumedAtUtc = state.ConsumedAtUtc?.UtcDateTime
            },
            cancellationToken: cancellationToken));
    }

    public async Task<GoogleCalendarOAuthState?> ConsumeOAuthStateAsync(
        string stateHash,
        DateTimeOffset consumedAtUtc,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.GoogleCalendarOAuthStates
            SET ConsumedAtUtc = @ConsumedAtUtc
            OUTPUT
                inserted.StateHash,
                inserted.TenantId,
                inserted.UserId,
                inserted.UserEmail,
                inserted.ExpiresAtUtc,
                inserted.CreatedAtUtc,
                inserted.ConsumedAtUtc
            WHERE StateHash = @StateHash
              AND ConsumedAtUtc IS NULL
              AND ExpiresAtUtc > @ConsumedAtUtc;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<GoogleCalendarOAuthStateRow>(
            new CommandDefinition(
                sql,
                new { StateHash = stateHash, ConsumedAtUtc = consumedAtUtc.UtcDateTime },
                cancellationToken: cancellationToken));

        return row?.ToDomain();
    }

    public async Task<GoogleCalendarConnection?> GetConnectionAsync(
        Guid tenantId,
        string provider,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                TenantId,
                OrganizerUserId,
                OrganizerEmail,
                Provider,
                RefreshTokenCiphertext,
                AccessTokenCiphertext,
                AccessTokenExpiresAtUtc,
                Scope,
                Status,
                ConnectedAtUtc,
                UpdatedAtUtc
            FROM dbo.GoogleCalendarConnections
            WHERE TenantId = @TenantId
              AND Provider = @Provider
              AND Status = N'Connected';
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<GoogleCalendarConnectionRow>(
            new CommandDefinition(sql, new { TenantId = tenantId, Provider = provider }, cancellationToken: cancellationToken));

        return row?.ToDomain();
    }

    public async Task SaveConnectionAsync(
        SaveGoogleCalendarConnectionInput input,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.GoogleCalendarConnections
            SET OrganizerUserId = @OrganizerUserId,
                OrganizerEmail = @OrganizerEmail,
                RefreshTokenCiphertext = COALESCE(@RefreshTokenCiphertext, RefreshTokenCiphertext),
                AccessTokenCiphertext = @AccessTokenCiphertext,
                AccessTokenExpiresAtUtc = @AccessTokenExpiresAtUtc,
                Scope = @Scope,
                Status = N'Connected',
                ConnectedAtUtc = @ConnectedAtUtc,
                UpdatedAtUtc = @UpdatedAtUtc
            WHERE TenantId = @TenantId
              AND Provider = @Provider;

            IF @@ROWCOUNT = 0
            BEGIN
                INSERT INTO dbo.GoogleCalendarConnections
                (
                    GoogleCalendarConnectionId,
                    TenantId,
                    OrganizerUserId,
                    OrganizerEmail,
                    Provider,
                    RefreshTokenCiphertext,
                    AccessTokenCiphertext,
                    AccessTokenExpiresAtUtc,
                    Scope,
                    Status,
                    ConnectedAtUtc,
                    UpdatedAtUtc
                )
                VALUES
                (
                    NEWID(),
                    @TenantId,
                    @OrganizerUserId,
                    @OrganizerEmail,
                    @Provider,
                    @RefreshTokenCiphertext,
                    @AccessTokenCiphertext,
                    @AccessTokenExpiresAtUtc,
                    @Scope,
                    N'Connected',
                    @ConnectedAtUtc,
                    @UpdatedAtUtc
                );
            END;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                input.TenantId,
                input.OrganizerUserId,
                input.OrganizerEmail,
                input.Provider,
                input.RefreshTokenCiphertext,
                input.AccessTokenCiphertext,
                AccessTokenExpiresAtUtc = input.AccessTokenExpiresAtUtc?.UtcDateTime,
                input.Scope,
                ConnectedAtUtc = input.ConnectedAtUtc.UtcDateTime,
                UpdatedAtUtc = input.UpdatedAtUtc.UtcDateTime
            },
            cancellationToken: cancellationToken));
    }

    public async Task UpdateAccessTokenAsync(
        Guid tenantId,
        string provider,
        string? accessTokenCiphertext,
        DateTimeOffset? accessTokenExpiresAtUtc,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.GoogleCalendarConnections
            SET AccessTokenCiphertext = @AccessTokenCiphertext,
                AccessTokenExpiresAtUtc = @AccessTokenExpiresAtUtc,
                UpdatedAtUtc = @UpdatedAtUtc
            WHERE TenantId = @TenantId
              AND Provider = @Provider
              AND Status = N'Connected';
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                Provider = provider,
                AccessTokenCiphertext = accessTokenCiphertext,
                AccessTokenExpiresAtUtc = accessTokenExpiresAtUtc?.UtcDateTime,
                UpdatedAtUtc = updatedAtUtc.UtcDateTime
            },
            cancellationToken: cancellationToken));
    }

    private static DateTimeOffset Utc(DateTime value)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    private static DateTimeOffset? ToUtc(DateTime? value)
    {
        return value.HasValue ? Utc(value.Value) : null;
    }

    private sealed class GoogleCalendarOAuthStateRow
    {
        public string StateHash { get; set; } = string.Empty;

        public Guid TenantId { get; set; }

        public Guid UserId { get; set; }

        public string UserEmail { get; set; } = string.Empty;

        public DateTime ExpiresAtUtc { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime? ConsumedAtUtc { get; set; }

        public GoogleCalendarOAuthState ToDomain()
        {
            return new GoogleCalendarOAuthState(
                StateHash,
                TenantId,
                UserId,
                UserEmail,
                Utc(ExpiresAtUtc),
                Utc(CreatedAtUtc),
                ToUtc(ConsumedAtUtc));
        }
    }

    private sealed class GoogleCalendarConnectionRow
    {
        public Guid TenantId { get; set; }

        public Guid OrganizerUserId { get; set; }

        public string OrganizerEmail { get; set; } = string.Empty;

        public string Provider { get; set; } = string.Empty;

        public string? RefreshTokenCiphertext { get; set; }

        public string? AccessTokenCiphertext { get; set; }

        public DateTime? AccessTokenExpiresAtUtc { get; set; }

        public string Scope { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public DateTime ConnectedAtUtc { get; set; }

        public DateTime UpdatedAtUtc { get; set; }

        public GoogleCalendarConnection ToDomain()
        {
            return new GoogleCalendarConnection(
                TenantId,
                OrganizerUserId,
                OrganizerEmail,
                Provider,
                RefreshTokenCiphertext,
                AccessTokenCiphertext,
                ToUtc(AccessTokenExpiresAtUtc),
                Scope,
                Status,
                Utc(ConnectedAtUtc),
                Utc(UpdatedAtUtc));
        }
    }
}
