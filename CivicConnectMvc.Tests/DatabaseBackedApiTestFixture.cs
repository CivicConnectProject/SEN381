using System.Data.Common;
using CivicConnectMvc.Data;
using CivicConnectMvc.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace CivicConnectMvc.Tests;

internal sealed class DatabaseBackedApiTestFixture : IDisposable
{
    private const string ApplicationConnectionStringEnvironmentVariable =
        "ConnectionStrings__CivicConnectDb";

    private static readonly SemaphoreSlim ActiveFixtureGate = new(1, 1);

    private readonly string _connectionString;
    private readonly SemaphoreSlim _requestGate = new(1, 1);
    private NpgsqlConnection? _connection;
    private CivicConnectDbContext? _database;
    private IDbContextTransaction? _transaction;
    private SharedConnectionApplicationFactory? _factory;
    private HttpClient? _client;
    private string? _previousApplicationConnectionString;
    private CreateRequestPrerequisites? _createRequestPrerequisites;
    private bool _applicationConnectionStringWasSet;
    private bool _ownsActiveFixtureGate;
    private bool _disposed;

    private DatabaseBackedApiTestFixture(string connectionString)
    {
        _connectionString = connectionString;
    }

    public static DatabaseBackedApiTestFixture Create()
    {
        var connectionString = PostgreSqlTestConnectionConfiguration.GetRequiredConnectionString();
        if (!ActiveFixtureGate.Wait(0))
        {
            throw new InvalidOperationException(
                "Database-backed HTTP fixtures are sequential-only; another fixture is already active.");
        }

        var fixture = new DatabaseBackedApiTestFixture(connectionString)
        {
            _ownsActiveFixtureGate = true
        };

        try
        {
            fixture.Initialize();
            return fixture;
        }
        catch (Exception initializationException)
        {
            try
            {
                fixture.Dispose();
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException(
                    "Database-backed HTTP fixture initialization and cleanup both failed.",
                    initializationException,
                    cleanupException);
            }

            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(initializationException)
                .Throw();
            throw;
        }
    }

    public HttpResponseMessage Send(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_requestGate.Wait(0))
        {
            throw new InvalidOperationException(
                "Database-backed HTTP requests must run sequentially on the shared PostgreSQL connection.");
        }

        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return (_client ?? throw new InvalidOperationException("The HTTP test client is unavailable."))
                .SendAsync(request)
                .GetAwaiter()
                .GetResult();
        }
        finally
        {
            _requestGate.Release();
        }
    }

    public (int RequesterId, int CategoryId) SeedCreateRequestPrerequisites()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_createRequestPrerequisites is { } existing)
            return (existing.RequesterId, existing.CategoryId);

        var database = _database
            ?? throw new InvalidOperationException("The fixture database context is unavailable.");
        if (_transaction is null)
            throw new InvalidOperationException("The fixture transaction is unavailable.");

        const int apiRequesterId = 1;
        var requesterExists = database.Users
            .AsNoTracking()
            .Any(user => user.UserId == apiRequesterId);

        var suffix = Guid.NewGuid().ToString("N");
        if (!requesterExists)
        {
            var requesterName = $"HTTP test requester {suffix}";
            var requesterEmail = $"http-requester-{suffix}@example.test";
            const string requesterRole = "Requester";
            database.Database.ExecuteSqlInterpolated(
                $"INSERT INTO public.users (user_id, name, email, role) OVERRIDING SYSTEM VALUE VALUES ({apiRequesterId}, {requesterName}, {requesterEmail}, {requesterRole})");
        }

        var category = new Category
        {
            Name = $"HTTP test category {suffix}",
            Description = "Temporary HTTP integration-test fixture"
        };
        database.Categories.Add(category);
        database.SaveChanges();

        _createRequestPrerequisites = new CreateRequestPrerequisites(apiRequesterId, category.CategoryId);
        return (_createRequestPrerequisites.RequesterId, _createRequestPrerequisites.CategoryId);
    }

    public CivicConnectDbContext CreateEnlistedContext()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var connection = _connection
            ?? throw new InvalidOperationException("The fixture database connection is unavailable.");
        var transaction = _transaction
            ?? throw new InvalidOperationException("The fixture transaction is unavailable.");

        var options = new DbContextOptionsBuilder<CivicConnectDbContext>()
            .UseNpgsql(connection, provider => provider.SetPostgresVersion(17, 0))
            .Options;
        var context = new CivicConnectDbContext(options);
        try
        {
            context.Database.UseTransaction(transaction.GetDbTransaction());
            return context;
        }
        catch
        {
            context.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        _requestGate.Wait();
        try
        {
            if (_disposed)
                return;

            _disposed = true;
            DisposeResources();
        }
        finally
        {
            _requestGate.Release();
        }
    }

    private void Initialize()
    {
        _previousApplicationConnectionString =
            Environment.GetEnvironmentVariable(ApplicationConnectionStringEnvironmentVariable);
        Environment.SetEnvironmentVariable(
            ApplicationConnectionStringEnvironmentVariable,
            _connectionString);
        _applicationConnectionStringWasSet = true;

        _connection = new NpgsqlConnection(_connectionString);
        _connection.Open();

        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "SELECT current_database()";
            var actualDatabase = command.ExecuteScalar() as string;
            if (!string.Equals(
                    actualDatabase,
                    PostgreSqlTestConnectionConfiguration.ExpectedDatabase,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"The connected PostgreSQL server reported database '{actualDatabase}', not the approved test database.");
            }
        }

        var options = new DbContextOptionsBuilder<CivicConnectDbContext>()
            .UseNpgsql(_connection, provider => provider.SetPostgresVersion(17, 0))
            .Options;
        _database = new CivicConnectDbContext(options);
        _transaction = _database.Database.BeginTransaction();

        _factory = new SharedConnectionApplicationFactory(
            _connection,
            _transaction,
            _connectionString);
        _client = _factory.CreateClient();

        var effectiveConnectionString = _factory.Services
            .GetRequiredService<IConfiguration>()
            .GetConnectionString("CivicConnectDb");
        if (string.IsNullOrWhiteSpace(effectiveConnectionString))
        {
            throw new InvalidOperationException(
                "The HTTP test host did not receive the required isolated PostgreSQL test connection string.");
        }

        if (!string.Equals(effectiveConnectionString, _connectionString, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The HTTP test host did not receive the required isolated PostgreSQL test connection string.");
        }

        _ = PostgreSqlTestConnectionConfiguration.Validate(effectiveConnectionString);
    }

    private void DisposeResources()
    {
        try
        {
            _client?.Dispose();
        }
        finally
        {
            try
            {
                _factory?.Dispose();
            }
            finally
            {
                try
                {
                    _transaction?.Rollback();
                }
                finally
                {
                    try
                    {
                        _transaction?.Dispose();
                    }
                    finally
                    {
                        try
                        {
                            _database?.Dispose();
                        }
                        finally
                        {
                            try
                            {
                                _connection?.Dispose();
                            }
                            finally
                            {
                                try
                                {
                                    if (_applicationConnectionStringWasSet)
                                    {
                                        Environment.SetEnvironmentVariable(
                                            ApplicationConnectionStringEnvironmentVariable,
                                            _previousApplicationConnectionString);
                                        _applicationConnectionStringWasSet = false;
                                    }
                                }
                                finally
                                {
                                    if (_ownsActiveFixtureGate)
                                    {
                                        _ownsActiveFixtureGate = false;
                                        ActiveFixtureGate.Release();
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private sealed class SharedConnectionApplicationFactory : WebApplicationFactory<global::Program>
    {
        private readonly NpgsqlConnection _connection;
        private readonly IDbContextTransaction _transaction;
        private readonly DbContextOptions<CivicConnectDbContext> _dbContextOptions;

        public SharedConnectionApplicationFactory(
            NpgsqlConnection connection,
            IDbContextTransaction transaction,
            string connectionString)
        {
            _connection = connection;
            _transaction = transaction;
            _ = PostgreSqlTestConnectionConfiguration.Validate(connectionString);
            _dbContextOptions = new DbContextOptionsBuilder<CivicConnectDbContext>()
                .UseNpgsql(_connection, provider => provider.SetPostgresVersion(17, 0))
                .Options;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<CivicConnectDbContext>();
                services.RemoveAll<DbContextOptions<CivicConnectDbContext>>();
                services.RemoveAll<DbContextOptions>();
                services.AddScoped(_ =>
                {
                    var context = new CivicConnectDbContext(_dbContextOptions);
                    try
                    {
                        context.Database.UseTransaction(_transaction.GetDbTransaction());
                        return context;
                    }
                    catch
                    {
                        context.Dispose();
                        throw;
                    }
                });
            });
        }
    }

    private sealed record CreateRequestPrerequisites(int RequesterId, int CategoryId);
}
