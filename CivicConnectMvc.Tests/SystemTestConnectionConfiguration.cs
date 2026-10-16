using System.Data.Common;
using System.Net;
using System.Net.Sockets;
using Npgsql;

namespace CivicConnectMvc.Tests;

internal sealed class SystemTestPreflightException : Exception
{
    public SystemTestPreflightException(string message)
        : base(message)
    {
    }

    public SystemTestPreflightException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

internal static class SystemTestConnectionConfiguration
{
    public const string EnvironmentVariable = "CIVICCONNECT_SYSTEM_TEST_CONNECTION_STRING";
    public const string ExpectedDatabase = "civicconnect_m3_system_test";

    private const string ApprovedHost = "localhost";
    private const int ApprovedPort = 5432;

    public static string GetRequiredConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new SystemTestPreflightException(
                $"{EnvironmentVariable} is not configured for this process.");

        return Validate(connectionString).ConnectionString;
    }

    public static NpgsqlConnectionStringBuilder Validate(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new SystemTestPreflightException("The system-test connection string is empty.");

        DbConnectionStringBuilder suppliedValues;
        try
        {
            suppliedValues = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };
        }
        catch (ArgumentException exception)
        {
            throw new SystemTestPreflightException(
                "The system-test connection string is malformed; its value is suppressed.",
                exception);
        }

        if (!suppliedValues.ContainsKey("Host") ||
            !suppliedValues.ContainsKey("Port") ||
            !suppliedValues.ContainsKey("Database"))
        {
            throw new SystemTestPreflightException(
                "The system-test connection must explicitly specify Host=localhost, Port=5432, and the dedicated database.");
        }

        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException exception)
        {
            throw new SystemTestPreflightException(
                "The system-test connection string is malformed; its value is suppressed.",
                exception);
        }

        if (!string.Equals(builder.Database, ExpectedDatabase, StringComparison.Ordinal))
        {
            throw new SystemTestPreflightException(
                $"The system-test connection must target {ExpectedDatabase}.");
        }

        if (!string.Equals(builder.Host, ApprovedHost, StringComparison.OrdinalIgnoreCase) ||
            builder.Port != ApprovedPort)
        {
            throw new SystemTestPreflightException(
                $"The system-test connection must target the approved local server {ApprovedHost}:{ApprovedPort}.");
        }

        builder.Pooling = false;
        return builder;
    }

    public static Uri CreateLoopbackBaseAddress()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var address = new Uri($"http://127.0.0.1:{port}/", UriKind.Absolute);
        if (address.Scheme != Uri.UriSchemeHttp ||
            address.Host != IPAddress.Loopback.ToString() ||
            address.Port is < 1 or > 65535 ||
            !string.IsNullOrEmpty(address.UserInfo) ||
            address.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(address.Query) ||
            !string.IsNullOrEmpty(address.Fragment))
        {
            throw new SystemTestPreflightException("The system-test HTTP endpoint is not a valid loopback URL.");
        }

        return address;
    }
}
