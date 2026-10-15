using System.Data.Common;
using Npgsql;

namespace CivicConnectMvc.Tests;

internal static class PostgreSqlTestConnectionConfiguration
{
    public const string EnvironmentVariable = "CIVICCONNECT_TEST_CONNECTION_STRING";
    public const string ExpectedDatabase = "civicconnect_m3_test";

    private const string ApprovedHost = "localhost";
    private const int ApprovedPort = 5432;

    public static string GetRequiredConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"{EnvironmentVariable} is not set.");

        return Validate(connectionString).ConnectionString;
    }

    public static NpgsqlConnectionStringBuilder Validate(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var suppliedValues = new DbConnectionStringBuilder
        {
            ConnectionString = connectionString
        };
        if (!suppliedValues.ContainsKey("Host") || !suppliedValues.ContainsKey("Port"))
        {
            throw new InvalidOperationException(
                "The PostgreSQL test connection must explicitly specify Host=localhost and Port=5432.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.Database, ExpectedDatabase, StringComparison.Ordinal))
            throw new InvalidOperationException($"The PostgreSQL test connection must target {ExpectedDatabase}.");

        if (!string.Equals(builder.Host, ApprovedHost, StringComparison.OrdinalIgnoreCase) ||
            builder.Port != ApprovedPort)
        {
            throw new InvalidOperationException(
                $"The PostgreSQL test connection must target the approved local server {ApprovedHost}:{ApprovedPort}.");
        }

        return builder;
    }
}
