using System.Data.Common;
using System.Security.Cryptography;
using CivicConnectMvc.Data;
using CivicConnectMvc.Infrastructure.Repositories;
using CivicConnectMvc.Interfaces;
using CivicConnectMvc.Models;
using CivicConnectMvc.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace CivicConnectMvc.Tests;

internal static class PostgreSqlPersistenceTests
{
    public static int RunAll()
    {
        try
        {
            PostgreSqlTestScope.ValidateConnectionConfiguration();
        }
        catch (Exception exception)
        {
            _ = exception;
            Console.WriteLine("PostgreSQL integration tests not run: test connection configuration is unavailable or invalid.");
            return 2;
        }

        var tests = new (string Name, Action Test)[]
        {
            (nameof(Request_Creation_And_Retrieval_Persist), Request_Creation_And_Retrieval_Persist),
            (nameof(Invalid_Request_Foreign_Keys_Are_Rejected), Invalid_Request_Foreign_Keys_Are_Rejected),
            (nameof(Assignment_Is_Persisted), Assignment_Is_Persisted),
            (nameof(Status_And_History_Save_Atomically), Status_And_History_Save_Atomically),
            (nameof(Transaction_Rollback_Removes_Test_Data), Transaction_Rollback_Removes_Test_Data),
            (nameof(Optimistic_Concurrency_Rejects_Stale_Write), Optimistic_Concurrency_Rejects_Stale_Write),
            (nameof(Resolution_Is_Persisted), Resolution_Is_Persisted)
        };

        var passed = 0;
        Console.WriteLine("CivicConnect PostgreSQL integration tests");
        Console.WriteLine("----------------------------------------");

        foreach (var (name, test) in tests)
        {
            try
            {
                test();
                Console.WriteLine($"PASS: {name}");
                passed++;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"FAIL: {name} ({exception.GetType().Name})");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"{passed}/{tests.Length} PostgreSQL integration tests passed.");
        return passed == tests.Length ? 0 : 1;
    }

    private static void Request_Creation_And_Retrieval_Persist()
    {
        using var scope = PostgreSqlTestScope.Begin();
        var service = scope.CreateService();
        var created = service.Create(NewRequest(scope.Category), scope.Requester.UserId);
        var requestId = created.RequestId;

        scope.Db.ChangeTracker.Clear();
        var loaded = service.GetById(requestId);

        Assert.NotNull(loaded);
        Assert.Equal(requestId, loaded!.RequestId);
        Assert.Equal(scope.Requester.UserId, loaded.RequesterId);
        Assert.Equal(scope.Category.CategoryId, loaded.CategoryId);
        Assert.Equal(RequestStatus.Submitted, loaded.CurrentStatus);
        Assert.Equal(1, loaded.RequestActions.Count);
    }

    private static void Invalid_Request_Foreign_Keys_Are_Rejected()
    {
        using var scope = PostgreSqlTestScope.Begin();
        var missingUserId = FindUnusedId(scope.Db.Users.Select(user => user.UserId).ToHashSet());
        var missingCategoryId = FindUnusedId(scope.Db.Categories.Select(category => category.CategoryId).ToHashSet());

        scope.Db.ServiceRequests.Add(NewRequestEntity(missingUserId, scope.Category.CategoryId));
        Assert.Throws<DbUpdateException>(() => scope.Db.SaveChanges());
        scope.Db.ChangeTracker.Clear();

        scope.Db.ServiceRequests.Add(NewRequestEntity(scope.Requester.UserId, missingCategoryId));
        Assert.Throws<DbUpdateException>(() => scope.Db.SaveChanges());
        scope.Db.ChangeTracker.Clear();

        Assert.Equal(0, scope.Db.ServiceRequests.Count());
    }

    private static void Assignment_Is_Persisted()
    {
        using var scope = PostgreSqlTestScope.Begin();
        var service = scope.CreateService();
        var request = service.Create(NewRequest(scope.Category), scope.Requester.UserId);

        service.Assign(request.RequestId, scope.Staff.UserId);
        scope.Db.ChangeTracker.Clear();

        var loaded = service.GetById(request.RequestId);
        Assert.NotNull(loaded);
        Assert.Equal(RequestStatus.Assigned, loaded!.CurrentStatus);
        Assert.Equal(scope.Staff.UserId, loaded.AssignedStaffId);
        Assert.Equal(1, loaded.Assignments.Count);
        Assert.Equal(true, loaded.Assignments.Single().Active);
        Assert.Equal(scope.Staff.UserId, loaded.Assignments.Single().StaffId);
    }

    private static void Status_And_History_Save_Atomically()
    {
        var interceptor = new FailOnStatusHistoryInsertInterceptor();
        using var scope = PostgreSqlTestScope.Begin(interceptor);
        var service = scope.CreateService();
        var request = service.Create(NewRequest(scope.Category), scope.Requester.UserId);
        service.Assign(request.RequestId, scope.Staff.UserId);
        scope.Db.ChangeTracker.Clear();

        var before = service.GetById(request.RequestId)!;
        var originalVersion = before.Version;
        var originalHistoryCount = before.StatusHistories.Count;
        var saveFailed = false;
        interceptor.Enabled = true;
        try
        {
            service.MoveToNextStatus(request.RequestId);
        }
        catch (Exception)
        {
            saveFailed = true;
        }
        finally
        {
            interceptor.Enabled = false;
        }

        Assert.Equal(true, saveFailed);
        Assert.Equal(true, interceptor.WasInjected);
        scope.Db.ChangeTracker.Clear();
        var after = service.GetById(request.RequestId);

        Assert.NotNull(after);
        Assert.Equal(RequestStatus.Assigned, after!.CurrentStatus);
        Assert.Equal(originalVersion, after.Version);
        Assert.Equal(originalHistoryCount, after.StatusHistories.Count);
        Assert.Equal(2, after.RequestActions.Count);
    }

    private static void Transaction_Rollback_Removes_Test_Data()
    {
        using var scope = PostgreSqlTestScope.Begin();
        var transaction = scope.Transaction;
        transaction.CreateSavepoint("request_rollback_test");

        var service = scope.CreateService();
        var created = service.Create(NewRequest(scope.Category), scope.Requester.UserId);
        var requestId = created.RequestId;
        scope.Db.ChangeTracker.Clear();
        Assert.NotNull(service.GetById(requestId));

        transaction.RollbackToSavepoint("request_rollback_test");
        scope.Db.ChangeTracker.Clear();

        Assert.Equal(null, service.GetById(requestId));
        Assert.Equal(0, scope.Db.RequestActions.Count());
    }

    private static void Optimistic_Concurrency_Rejects_Stale_Write()
    {
        using var scope = PostgreSqlTestScope.Begin();
        var firstService = scope.CreateService();
        var request = firstService.Create(NewRequest(scope.Category), scope.Requester.UserId);
        firstService.Assign(request.RequestId, scope.Staff.UserId);

        using var secondDb = scope.CreateSiblingContext();
        var secondService = CreateService(secondDb);
        var firstSnapshot = firstService.GetById(request.RequestId)!;
        var secondSnapshot = secondService.GetById(request.RequestId)!;
        var originalVersion = firstSnapshot.Version;
        Assert.Equal(originalVersion, secondSnapshot.Version);

        firstService.MoveToNextStatus(request.RequestId);
        Assert.Throws<InvalidOperationException>(() => secondService.MoveToNextStatus(request.RequestId));

        scope.Db.ChangeTracker.Clear();
        secondDb.ChangeTracker.Clear();
        var persisted = firstService.GetById(request.RequestId);

        Assert.NotNull(persisted);
        Assert.Equal(RequestStatus.InProgress, persisted!.CurrentStatus);
        Assert.Equal(originalVersion + 1, persisted.Version);
        Assert.Equal(2, persisted.StatusHistories.Count);
        Assert.Equal(3, persisted.RequestActions.Count);
    }

    private static void Resolution_Is_Persisted()
    {
        using var scope = PostgreSqlTestScope.Begin();
        var service = scope.CreateService();
        var request = service.Create(NewRequest(scope.Category), scope.Requester.UserId);
        service.Assign(request.RequestId, scope.Staff.UserId);
        service.MoveToNextStatus(request.RequestId);
        service.MoveToNextStatus(request.RequestId);
        scope.Db.ChangeTracker.Clear();

        var loaded = service.GetById(request.RequestId);
        Assert.NotNull(loaded);
        Assert.Equal(RequestStatus.Resolved, loaded!.CurrentStatus);
        Assert.NotNull(loaded.Resolution);
        Assert.Equal(scope.Requester.UserId, loaded.Resolution!.ResolvedBy);
        Assert.Equal(3, loaded.StatusHistories.Count);
    }

    internal static RequestService CreateService(CivicConnectDbContext db) =>
        new(new ServiceRequestRepository(db), Array.Empty<IRequestObserver>());

    private static ServiceRequestViewModel NewRequest(Category category) => new()
    {
        Title = $"Integration request {Guid.NewGuid():N}",
        Description = "Created by an isolated PostgreSQL integration test.",
        CategoryId = category.CategoryId
    };

    private static ServiceRequest NewRequestEntity(int requesterId, int categoryId) => new()
    {
        RequesterId = requesterId,
        CategoryId = categoryId,
        Title = $"Invalid FK request {Guid.NewGuid():N}",
        Description = "This row must be rejected by a foreign-key constraint."
    };

    private static int FindUnusedId(HashSet<int> existingIds)
    {
        int candidate;
        do
        {
            candidate = RandomNumberGenerator.GetInt32(1, int.MaxValue);
        }
        while (existingIds.Contains(candidate));

        return candidate;
    }

    private static class Assert
    {
        public static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException("Assertion failed: values are not equal.");
        }

        public static void NotNull(object? value)
        {
            if (value is null)
                throw new InvalidOperationException("Assertion failed: value was null.");
        }

        public static void Throws<TException>(Action action) where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            throw new InvalidOperationException($"Assertion failed: {typeof(TException).Name} was not thrown.");
        }
    }
}

internal sealed class PostgreSqlTestScope : IDisposable
{
    private const string ExpectedDatabase = "civicconnect_m3_test";
    private readonly NpgsqlConnection _connection;
    private readonly DbContextOptions<CivicConnectDbContext> _options;
    private IDbContextTransaction? _transaction;

    private PostgreSqlTestScope(string connectionString, DbCommandInterceptor? interceptor)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.Database, ExpectedDatabase, StringComparison.Ordinal))
            throw new InvalidOperationException("The integration-test connection must target civicconnect_m3_test.");

        _connection = new NpgsqlConnection(builder.ConnectionString);
        var optionsBuilder = new DbContextOptionsBuilder<CivicConnectDbContext>()
            .UseNpgsql(_connection, provider => provider.SetPostgresVersion(17, 0));
        if (interceptor is not null)
            optionsBuilder.AddInterceptors(interceptor);

        _options = optionsBuilder.Options;
        Db = new CivicConnectDbContext(_options);
    }

    public CivicConnectDbContext Db { get; }
    public User Requester { get; private set; } = null!;
    public User Staff { get; private set; } = null!;
    public Category Category { get; private set; } = null!;
    public IDbContextTransaction Transaction => _transaction!;

    public static void ValidateConnectionConfiguration()
    {
        var connectionString = Environment.GetEnvironmentVariable("CIVICCONNECT_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("CIVCONNECT_TEST_CONNECTION_STRING is not set.");

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.Database, ExpectedDatabase, StringComparison.Ordinal))
            throw new InvalidOperationException("The integration-test connection must target civicconnect_m3_test.");
    }

    public static PostgreSqlTestScope Begin(DbCommandInterceptor? interceptor = null)
    {
        var connectionString = Environment.GetEnvironmentVariable("CIVICCONNECT_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("CIVCONNECT_TEST_CONNECTION_STRING is not set.");

        var scope = new PostgreSqlTestScope(connectionString, interceptor);
        try
        {
            scope._transaction = scope.Db.Database.BeginTransaction();
            scope.SeedFixtureRows();
            return scope;
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    public RequestService CreateService() => PostgreSqlPersistenceTests.CreateService(Db);

    public CivicConnectDbContext CreateSiblingContext()
    {
        var context = new CivicConnectDbContext(_options);
        try
        {
            context.Database.UseTransaction(Transaction.GetDbTransaction());
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
                Db.Dispose();
                _connection.Dispose();
            }
        }
    }

    private void SeedFixtureRows()
    {
        var suffix = Guid.NewGuid().ToString("N");
        Requester = new User
        {
            Name = $"Test requester {suffix}",
            Email = $"requester-{suffix}@example.test",
            Role = UserRole.Requester
        };
        Staff = new User
        {
            Name = $"Test staff {suffix}",
            Email = $"staff-{suffix}@example.test",
            Role = UserRole.Staff
        };
        Category = new Category
        {
            Name = $"Integration category {suffix}",
            Description = "Temporary integration-test fixture"
        };

        Db.Users.AddRange(Requester, Staff);
        Db.Categories.Add(Category);
        Db.SaveChanges();
    }
}

internal sealed class FailOnStatusHistoryInsertInterceptor : DbCommandInterceptor
{
    public bool Enabled { get; set; }
    public bool WasInjected { get; private set; }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        ThrowIfStatusHistoryInsert(command);
        return result;
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        ThrowIfStatusHistoryInsert(command);
        return result;
    }

    private void ThrowIfStatusHistoryInsert(DbCommand command)
    {
        if (Enabled &&
            command.CommandText.Contains("INSERT", StringComparison.OrdinalIgnoreCase) &&
            command.CommandText.Contains("status_history", StringComparison.OrdinalIgnoreCase))
        {
            WasInjected = true;
            throw new InvalidOperationException("Injected status-history persistence failure.");
        }
    }
}