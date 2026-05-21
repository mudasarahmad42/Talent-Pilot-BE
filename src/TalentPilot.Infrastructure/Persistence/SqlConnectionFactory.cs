using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace TalentPilot.Infrastructure.Persistence;

public interface ISqlConnectionFactory
{
    SqlConnection CreateConnection();
}

public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("TalentPilot")
            ?? throw new InvalidOperationException("ConnectionStrings:TalentPilot is missing.");
    }

    public SqlConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }
}
