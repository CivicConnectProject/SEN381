using System.Data;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CivicConnectMvc.Models;
using Npgsql;
using NpgsqlTypes;

namespace CivicConnectMvc.Tests;

internal static class SystemHttpTests
{
    public static int RunAll()
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        SystemHttpTestFixture fixture;
        try
        {
            fixture = SystemHttpTestFixture.Create(cancellation.Token);
        }
        catch (Exception exception)
        {
            Console.CancelKeyPress -= cancelHandler;
            Console.Error.WriteLine($"System HTTP tests not started: {DescribeSafeSetupFailure(exception)}");
            return 2;
        }

        var tests = new (string Name, Action Test)[]
        {
            (nameof(SystemHttpTestFixture.Successful_Request_Lifecycle_Uses_Loopback_Http), fixture.Successful_Request_Lifecycle_Uses_Loopback_Http),
            (nameof(SystemHttpTestFixture.Invalid_Transition_Is_Rejected_Over_Loopback_Http), fixture.Invalid_Transition_Is_Rejected_Over_Loopback_Http)
        };
        var passed = 0;

        Console.WriteLine("CivicConnect external-process HTTP system tests");
        Console.WriteLine("-----------------------------------------------");

        var cleanupFailed = false;
        try
        {
            foreach (var (name, test) in tests)
            {
                if (cancellation.IsCancellationRequested)
                    break;

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
        }
        finally
        {
            try
            {
                fixture.Dispose();
            }
            catch (Exception exception)
            {
                cleanupFailed = true;
                Console.Error.WriteLine($"System HTTP fixture cleanup failed ({exception.GetType().Name}).");
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"{passed}/{tests.Length} system HTTP tests passed.");
        return passed == tests.Length && !cleanupFailed ? 0 : 1;
    }

    private static string DescribeSafeSetupFailure(Exception exception)
    {
        var preflightException = FindException<SystemTestPreflightException>(exception);
        if (preflightException is not null)
            return preflightException.Message;

        var postgresException = FindException<NpgsqlException>(exception);
        if (postgresException?.SqlState is { } sqlState)
            return $"PostgreSQL setup failed (SQLSTATE {sqlState}); provider details suppressed.";

        return $"Setup failed ({exception.GetType().Name}); exception details suppressed.";
    }

    private static TException? FindException<TException>(Exception exception)
        where TException : Exception
    {
        if (exception is TException match)
            return match;

        if (exception is AggregateException aggregate)
        {
            foreach (var innerException in aggregate.InnerExceptions)
            {
                var nestedMatch = FindException<TException>(innerException);
                if (nestedMatch is not null)
                    return nestedMatch;
            }

            return null;
        }

        return exception.InnerException is null
            ? null
            : FindException<TException>(exception.InnerException);
    }
}

internal sealed class SystemHttpTestFixture : IDisposable
{
    private const int RequesterId = 1;
    private const int DatabasePort = 5432;
    private static readonly TimeSpan ApplicationStartupTimeout = TimeSpan.FromSeconds(30);
    private readonly object _listenerGate = new();
    private readonly TaskCompletionSource<bool> _loopbackListenerStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<string> _reportedListeners = [];

    private readonly string _connectionString;
    private readonly CancellationToken _cancellationToken;
    private readonly string _runTag = Guid.NewGuid().ToString("N");
    private readonly List<string> _requestTitles = [];
    private readonly HashSet<int> _responseRequestIds = [];
    private NpgsqlConnection? _database;
    private Process? _application;
    private HttpClient? _client;
    private int? _categoryId;
    private int? _staffId;
    private string? _categoryName;
    private string? _staffName;
    private string? _staffEmail;
    private bool _disposed;

    private SystemHttpTestFixture(string connectionString, CancellationToken cancellationToken)
    {
        _connectionString = connectionString;
        _cancellationToken = cancellationToken;
        BaseAddress = SystemTestConnectionConfiguration.CreateLoopbackBaseAddress();
    }

    private Uri BaseAddress { get; }

    public static SystemHttpTestFixture Create(CancellationToken cancellationToken)
    {
        var connectionString = SystemTestConnectionConfiguration.GetRequiredConnectionString();
        var fixture = new SystemHttpTestFixture(connectionString, cancellationToken);
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
                    "System-test initialization and cleanup both failed.",
                    initializationException,
                    cleanupException);
            }

            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(initializationException)
                .Throw();
            throw;
        }
    }

    public void Successful_Request_Lifecycle_Uses_Loopback_Http()
    {
        var title = $"CivicConnect system {_runTag} lifecycle";
        var requestId = CreateRequest(title);
        AssertStatus(requestId, "Submitted");

        using (var assignResponse = Send(HttpMethod.Post, $"/api/servicerequests/{requestId}/assign/{_staffId}"))
        {
            AssertStatusCode(HttpStatusCode.NoContent, assignResponse.StatusCode);
        }
        AssertStatus(requestId, "Assigned");

        foreach (var expectedStatus in new[] { "InProgress", "Resolved", "Closed" })
        {
            using var advanceResponse = Send(HttpMethod.Post, $"/api/servicerequests/{requestId}/advance");
            AssertStatusCode(HttpStatusCode.NoContent, advanceResponse.StatusCode);
            AssertStatus(requestId, expectedStatus);
        }

        using var finalResponse = Send(HttpMethod.Get, $"/api/servicerequests/{requestId}");
        AssertStatusCode(HttpStatusCode.OK, finalResponse.StatusCode);
        using var finalDocument = JsonDocument.Parse(finalResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        var finalRequest = finalDocument.RootElement;
        AssertEqual("Closed", finalRequest.GetProperty("currentStatusText").GetString());
        AssertEqual(_staffId, finalRequest.GetProperty("assignedStaffId").GetInt32());
    }

    public void Invalid_Transition_Is_Rejected_Over_Loopback_Http()
    {
        var title = $"CivicConnect system {_runTag} invalid transition";
        var requestId = CreateRequest(title);

        using (var advanceResponse = Send(HttpMethod.Post, $"/api/servicerequests/{requestId}/advance"))
        {
            AssertStatusCode(HttpStatusCode.BadRequest, advanceResponse.StatusCode);
            using var errorDocument = JsonDocument.Parse(
                advanceResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            AssertEqual(
                "The request must be assigned before its work can progress.",
                errorDocument.RootElement.GetProperty("error").GetString());
        }

        AssertStatus(requestId, "Submitted");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Exception? stopException = null;
        try
        {
            StopApplication();
        }
        catch (Exception exception)
        {
            stopException = exception;
        }

        Exception? cleanupException = null;
        try
        {
            if (stopException is null)
                CleanupData();
            else
                cleanupException = new InvalidOperationException(
                    "Database cleanup was skipped because the application process did not stop.");
        }
        catch (Exception exception)
        {
            cleanupException = exception;
        }
        finally
        {
            _database?.Dispose();
        }

        if (stopException is not null && cleanupException is not null)
        {
            throw new AggregateException(
                "System-test application shutdown and data cleanup both failed.",
                stopException,
                cleanupException);
        }

        if (stopException is not null)
            throw new InvalidOperationException("The system-test application could not be stopped.", stopException);

        if (cleanupException is not null)
            throw new InvalidOperationException("System-test data cleanup failed.", cleanupException);
    }

    private void Initialize()
    {
        _database = new NpgsqlConnection(_connectionString);
        _database.Open();
        VerifyDatabaseIdentity();
        VerifyRequiredSchema();
        VerifyRequester();
        VerifyUserIdentitySequence();

        _categoryName = $"CivicConnect system {_runTag} category";
        _staffName = $"CivicConnect system {_runTag} staff";
        _staffEmail = $"system-{_runTag}@example.test";
        _categoryId = InsertCategory(_categoryName);
        _staffId = InsertStaff(_staffName, _staffEmail);

        StartApplication();
    }

    private void VerifyDatabaseIdentity()
    {
        using var command = new NpgsqlCommand(
            "SELECT current_database(), inet_server_addr(), inet_server_port()",
            _database);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            throw new SystemTestPreflightException("Database identity check failed: PostgreSQL returned no identity row.");

        var databaseName = reader.GetString(0);
        if (!string.Equals(
                databaseName,
                SystemTestConnectionConfiguration.ExpectedDatabase,
                StringComparison.Ordinal))
        {
            throw new SystemTestPreflightException(
                $"Database identity check failed: current_database() must be {SystemTestConnectionConfiguration.ExpectedDatabase}.");
        }

        if (reader.IsDBNull(1))
            throw new SystemTestPreflightException(
                "Database identity check failed: inet_server_addr() returned NULL, so the server cannot be verified as loopback.");

        var serverAddress = reader.GetFieldValue<IPAddress>(1);

        if (!IPAddress.IsLoopback(serverAddress))
            throw new SystemTestPreflightException(
                "Database identity check failed: inet_server_addr() is not a loopback address.");

        var serverPort = reader.GetInt32(2);
        if (serverPort != DatabasePort)
            throw new SystemTestPreflightException(
                $"Database identity check failed: inet_server_port() must be {DatabasePort}.");
    }

    private void VerifyRequiredSchema()
    {
        using var command = new NpgsqlCommand(
            """
            SELECT to_regclass('public.users') IS NOT NULL
               AND to_regclass('public.categories') IS NOT NULL
               AND to_regclass('public.service_requests') IS NOT NULL
               AND to_regclass('public.assignments') IS NOT NULL
               AND to_regclass('public.status_history') IS NOT NULL
               AND to_regclass('public.request_actions') IS NOT NULL
               AND to_regclass('public.resolutions') IS NOT NULL
            """,
            _database);
        if (!Equals(command.ExecuteScalar(), true))
            throw new SystemTestPreflightException("The dedicated system-test database schema is incomplete.");
    }

    private void VerifyRequester()
    {
        using var command = new NpgsqlCommand(
            "SELECT role FROM public.users WHERE user_id = @user_id",
            _database);
        command.Parameters.AddWithValue("user_id", RequesterId);
        var role = command.ExecuteScalar() as string;
        if (!string.Equals(role, UserRole.Requester.ToString(), StringComparison.Ordinal))
        {
            throw new SystemTestPreflightException(
                "System tests require an existing user ID 1 with the Requester role; the harness will not create or modify it.");
        }
    }

    private void VerifyUserIdentitySequence()
    {
        using var command = new NpgsqlCommand(
            "SELECT last_value, is_called FROM public.users_user_id_seq",
            _database);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            throw new SystemTestPreflightException("The users identity sequence could not be verified.");

        var lastValue = reader.GetInt64(0);
        var isCalled = reader.GetBoolean(1);
        reader.Close();

        using var maxIdCommand = new NpgsqlCommand(
            "SELECT COALESCE(MAX(user_id), 0) FROM public.users",
            _database);
        var maxUserId = Convert.ToInt64(maxIdCommand.ExecuteScalar());
        var nextValue = isCalled ? lastValue + 1 : lastValue;
        if (nextValue <= maxUserId || nextValue > int.MaxValue)
        {
            throw new SystemTestPreflightException(
                "The users identity sequence must have a valid next value beyond the existing user IDs.");
        }
    }

    private int InsertCategory(string name)
    {
        using var command = new NpgsqlCommand(
            """
            INSERT INTO public.categories (name, description)
            VALUES (@name, @description)
            RETURNING category_id
            """,
            _database);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("description", "Temporary synthetic system-test category");
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private int InsertStaff(string name, string email)
    {
        using var command = new NpgsqlCommand(
            """
            INSERT INTO public.users (name, email, role)
            VALUES (@name, @email, @role)
            RETURNING user_id
            """,
            _database);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("email", email);
        command.Parameters.AddWithValue("role", UserRole.Staff.ToString());
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private void StartApplication()
    {
        var repositoryRoot = FindRepositoryRoot();
        var applicationDirectory = Path.Combine(repositoryRoot, "CivicConnectMvc");
        var releaseAssembly = Path.Combine(
            applicationDirectory,
            "bin",
            "Release",
            "net10.0",
            "CivicConnectMvc.dll");
        if (!File.Exists(releaseAssembly))
        {
            throw new SystemTestPreflightException(
                "Build the CivicConnect solution in Release configuration before running --system.");
        }

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = applicationDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(releaseAssembly);

        foreach (var key in startInfo.Environment.Keys
                     .Where(key =>
                         key.StartsWith("ASPNETCORE_Kestrel__Endpoints", StringComparison.OrdinalIgnoreCase) ||
                         key.StartsWith("Kestrel__Endpoints", StringComparison.OrdinalIgnoreCase) ||
                         key.Equals("ASPNETCORE_HTTP_PORTS", StringComparison.OrdinalIgnoreCase) ||
                         key.Equals("ASPNETCORE_HTTPS_PORTS", StringComparison.OrdinalIgnoreCase) ||
                         key.Equals("ASPNETCORE_HTTPS_PORT", StringComparison.OrdinalIgnoreCase))
                     .ToArray())
        {
            startInfo.Environment.Remove(key);
        }

        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";
        startInfo.Environment["ASPNETCORE_URLS"] = BaseAddress.GetLeftPart(UriPartial.Authority);
        startInfo.Environment["ConnectionStrings__CivicConnectDb"] = _connectionString;
        startInfo.Environment["DOTNET_NOLOGO"] = "true";

        var expectedListener = BaseAddress.GetLeftPart(UriPartial.Authority);
        void ObserveListener(string? line)
        {
            const string listeningMarker = "Now listening on:";
            if (line is null)
                return;

            var markerIndex = line.IndexOf(listeningMarker, StringComparison.Ordinal);
            if (markerIndex >= 0)
            {
                var actualListener = line[(markerIndex + listeningMarker.Length)..].Trim();
                lock (_listenerGate)
                    _reportedListeners.Add(actualListener);
                _loopbackListenerStarted.TrySetResult(
                    string.Equals(actualListener, expectedListener, StringComparison.Ordinal));
            }
        }

        var application = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        application.OutputDataReceived += (_, eventArgs) => ObserveListener(eventArgs.Data);
        application.ErrorDataReceived += (_, eventArgs) => ObserveListener(eventArgs.Data);
        if (!application.Start())
        {
            application.Dispose();
            throw new SystemTestPreflightException("The system-test application process did not start.");
        }

        _application = application;
        _application.BeginOutputReadLine();
        _application.BeginErrorReadLine();

        _client = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false })
        {
            BaseAddress = BaseAddress,
            Timeout = TimeSpan.FromSeconds(15)
        };
        WaitUntilReady();
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CivicConnectMvc.sln")) &&
                File.Exists(Path.Combine(directory.FullName, "CivicConnectMvc", "CivicConnectMvc.csproj")))
            {
                return directory.FullName;
            }
        }

        throw new SystemTestPreflightException("The repository root could not be located from the test runner.");
    }

    private void WaitUntilReady()
    {
        var deadline = Stopwatch.StartNew();
        while (!_loopbackListenerStarted.Task.IsCompleted &&
               deadline.Elapsed < ApplicationStartupTimeout)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (_application is null || _application.HasExited)
                throw new SystemTestPreflightException(
                    "The system-test application exited before binding its loopback URL.");
            Thread.Sleep(50);
        }

        if (!_loopbackListenerStarted.Task.IsCompletedSuccessfully ||
            !_loopbackListenerStarted.Task.Result)
        {
            throw new SystemTestPreflightException(
                "The application did not confirm the required loopback-only HTTP binding.");
        }

        while (deadline.Elapsed < ApplicationStartupTimeout)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (_application is null || _application.HasExited)
                throw new SystemTestPreflightException("The system-test application exited before becoming ready.");

            try
            {
                using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    _cancellationToken,
                    cancellation.Token);
                using var response = _client!.GetAsync(
                        "/api/servicerequests",
                        linkedCancellation.Token)
                    .GetAwaiter()
                    .GetResult();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    if (_application.HasExited)
                        throw new SystemTestPreflightException(
                            "The system-test application process exited during startup.");
                    Thread.Sleep(100);
                    lock (_listenerGate)
                    {
                        if (_reportedListeners.Count != 1 ||
                            !string.Equals(_reportedListeners[0], BaseAddress.GetLeftPart(UriPartial.Authority), StringComparison.Ordinal))
                        {
                            throw new SystemTestPreflightException(
                                "The system-test application reported a non-loopback or unexpected listener.");
                        }
                    }
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
                _cancellationToken.ThrowIfCancellationRequested();
            }

            Thread.Sleep(100);
        }

        throw new SystemTestPreflightException("The loopback system-test application did not become ready.");
    }

    private int CreateRequest(string title)
    {
        _requestTitles.Add(title);
        using var response = _client!.PostAsJsonAsync(
                "/api/servicerequests",
                new
                {
                    title,
                    description = $"Synthetic system-test request {_runTag}.",
                    categoryId = _categoryId
                },
                _cancellationToken)
            .GetAwaiter()
            .GetResult();
        AssertStatusCode(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        var root = document.RootElement;
        AssertEqual(title, root.GetProperty("title").GetString());
        var requestId = root.GetProperty("requestId").GetInt32();
        if (requestId <= 0)
            throw new InvalidOperationException("The API returned an invalid service-request ID.");

        var location = response.Headers.Location
            ?? throw new InvalidOperationException("The API omitted the created request Location header.");
        var locationUri = location.IsAbsoluteUri ? location : new Uri(BaseAddress, location);
        if (locationUri.Scheme != Uri.UriSchemeHttp ||
            locationUri.Host != BaseAddress.Host ||
            locationUri.Port != BaseAddress.Port ||
            locationUri.AbsolutePath != $"/api/servicerequests/{requestId}")
        {
            throw new InvalidOperationException("The API returned a Location outside the loopback request URL.");
        }

        _responseRequestIds.Add(requestId);
        return requestId;
    }

    private void AssertStatus(int requestId, string expectedStatus)
    {
        using var response = Send(HttpMethod.Get, $"/api/servicerequests/{requestId}");
        AssertStatusCode(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        AssertEqual(expectedStatus, document.RootElement.GetProperty("currentStatusText").GetString());
    }

    private HttpResponseMessage Send(HttpMethod method, string path)
    {
        using var request = new HttpRequestMessage(method, path);
        return _client!.SendAsync(request, _cancellationToken).GetAwaiter().GetResult();
    }

    private static void AssertStatusCode(HttpStatusCode expected, HttpStatusCode actual)
    {
        if (expected != actual)
            throw new InvalidOperationException($"Expected HTTP {(int)expected}, received {(int)actual}.");
    }

    private static void AssertEqual<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException("A system-test response did not match the expected value.");
    }

    private void StopApplication()
    {
        _client?.Dispose();
        _client = null;

        if (_application is null)
            return;

        try
        {
            if (!_application.HasExited)
            {
                _application.Kill(entireProcessTree: true);
                if (!_application.WaitForExit(10_000))
                    throw new TimeoutException("The system-test application process did not stop.");
            }
        }
        finally
        {
            _application.Dispose();
            _application = null;
        }
    }

    private void CleanupData()
    {
        if (_database is null || _database.State != System.Data.ConnectionState.Open)
            return;

        using var transaction = _database.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            var requestIds = FindOwnedRequestIds(transaction);
            if (_responseRequestIds.Any(id => !requestIds.Contains(id)))
                throw new InvalidOperationException("A created service request could not be identified for exact-ID cleanup.");

            EnsureNoOutsideReferences(transaction, requestIds);

            var assignmentIds = ReadRelatedIds(
                transaction,
                "SELECT assignment_id FROM public.assignments WHERE request_id = ANY(@request_ids)",
                requestIds);
            var historyIds = ReadRelatedIds(
                transaction,
                "SELECT status_history_id FROM public.status_history WHERE request_id = ANY(@request_ids)",
                requestIds);
            var actionIds = ReadRelatedIds(
                transaction,
                "SELECT action_id FROM public.request_actions WHERE request_id = ANY(@request_ids)",
                requestIds);
            var resolutionIds = ReadRelatedIds(
                transaction,
                "SELECT resolution_id FROM public.resolutions WHERE request_id = ANY(@request_ids)",
                requestIds);

            DeleteExactIds(
                transaction,
                "DELETE FROM public.assignments WHERE assignment_id = ANY(@ids)",
                assignmentIds);
            DeleteExactIds(
                transaction,
                "DELETE FROM public.status_history WHERE status_history_id = ANY(@ids)",
                historyIds);
            DeleteExactIds(
                transaction,
                "DELETE FROM public.request_actions WHERE action_id = ANY(@ids)",
                actionIds);
            DeleteExactIds(
                transaction,
                "DELETE FROM public.resolutions WHERE resolution_id = ANY(@ids)",
                resolutionIds);
            DeleteExactIds(
                transaction,
                "DELETE FROM public.service_requests WHERE request_id = ANY(@ids)",
                requestIds);

            DeleteSyntheticCategory(transaction);
            DeleteSyntheticStaff(transaction);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private int[] FindOwnedRequestIds(NpgsqlTransaction transaction)
    {
        if (_requestTitles.Count == 0)
            return [];

        using var command = new NpgsqlCommand(
            """
            SELECT request_id, requester_id, category_id, title
            FROM public.service_requests
            WHERE title = ANY(@titles)
            """,
            _database,
            transaction);
        command.Parameters.AddWithValue("titles", NpgsqlDbType.Array | NpgsqlDbType.Text, _requestTitles.ToArray());

        var ids = new List<int>();
        var foundTitles = new HashSet<string>(StringComparer.Ordinal);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var title = reader.GetString(3);
            if (!_requestTitles.Contains(title, StringComparer.Ordinal) ||
                !foundTitles.Add(title) ||
                reader.GetInt32(1) != RequesterId ||
                reader.GetInt32(2) != _categoryId)
            {
                throw new InvalidOperationException("A tagged request does not match the system-test ownership data.");
            }

            ids.Add(reader.GetInt32(0));
        }

        return ids.ToArray();
    }

    private void EnsureNoOutsideReferences(NpgsqlTransaction transaction, int[] requestIds)
    {
        if (_categoryId is { } categoryId)
        {
            using var command = new NpgsqlCommand(
                """
                SELECT EXISTS (
                    SELECT 1 FROM public.service_requests
                    WHERE category_id = @id AND NOT (request_id = ANY(@request_ids))
                )
                """,
                _database,
                transaction);
            command.Parameters.AddWithValue("id", categoryId);
            AddRequestIds(command, requestIds);
            if (Equals(command.ExecuteScalar(), true))
                throw new InvalidOperationException("The synthetic category has references outside this test run.");
        }

        if (_staffId is { } staffId)
        {
            using var command = new NpgsqlCommand(
                """
                SELECT EXISTS (
                    SELECT 1 FROM public.assignments
                    WHERE staff_id = @id AND NOT (request_id = ANY(@request_ids))
                    UNION ALL
                    SELECT 1 FROM public.request_actions
                    WHERE user_id = @id AND NOT (request_id = ANY(@request_ids))
                    UNION ALL
                    SELECT 1 FROM public.status_history
                    WHERE changed_by = @id AND NOT (request_id = ANY(@request_ids))
                    UNION ALL
                    SELECT 1 FROM public.resolutions
                    WHERE resolved_by = @id AND NOT (request_id = ANY(@request_ids))
                    UNION ALL
                    SELECT 1 FROM public.service_requests
                    WHERE requester_id = @id AND NOT (request_id = ANY(@request_ids))
                )
                """,
                _database,
                transaction);
            command.Parameters.AddWithValue("id", staffId);
            AddRequestIds(command, requestIds);
            if (Equals(command.ExecuteScalar(), true))
                throw new InvalidOperationException("The synthetic staff user has references outside this test run.");
        }
    }

    private int[] ReadRelatedIds(
        NpgsqlTransaction transaction,
        string commandText,
        int[] requestIds)
    {
        if (requestIds.Length == 0)
            return [];

        using var command = new NpgsqlCommand(commandText, _database, transaction);
        AddRequestIds(command, requestIds);
        using var reader = command.ExecuteReader();
        var ids = new List<int>();
        while (reader.Read())
            ids.Add(reader.GetInt32(0));
        return ids.ToArray();
    }

    private void DeleteExactIds(
        NpgsqlTransaction transaction,
        string commandText,
        int[] ids)
    {
        if (ids.Length == 0)
            return;

        using var command = new NpgsqlCommand(commandText, _database, transaction);
        command.Parameters.AddWithValue("ids", NpgsqlDbType.Array | NpgsqlDbType.Integer, ids);
        if (command.ExecuteNonQuery() != ids.Length)
            throw new InvalidOperationException("Exact-ID system-test cleanup affected an unexpected number of rows.");
    }

    private void DeleteSyntheticCategory(NpgsqlTransaction transaction)
    {
        if (_categoryId is not { } categoryId || _categoryName is null)
            return;

        using var command = new NpgsqlCommand(
            """
            DELETE FROM public.categories
            WHERE category_id = @id
              AND name = @name
              AND NOT EXISTS (
                  SELECT 1 FROM public.service_requests WHERE category_id = @id
              )
            """,
            _database,
            transaction);
        command.Parameters.AddWithValue("id", categoryId);
        command.Parameters.AddWithValue("name", _categoryName);
        if (command.ExecuteNonQuery() != 1)
            throw new InvalidOperationException("The exact synthetic category could not be safely removed.");
    }

    private void DeleteSyntheticStaff(NpgsqlTransaction transaction)
    {
        if (_staffId is not { } staffId || _staffName is null || _staffEmail is null)
            return;

        using var command = new NpgsqlCommand(
            """
            DELETE FROM public.users
            WHERE user_id = @id
              AND name = @name
              AND email = @email
              AND role = @role
              AND NOT EXISTS (SELECT 1 FROM public.assignments WHERE staff_id = @id)
              AND NOT EXISTS (SELECT 1 FROM public.request_actions WHERE user_id = @id)
              AND NOT EXISTS (SELECT 1 FROM public.status_history WHERE changed_by = @id)
              AND NOT EXISTS (SELECT 1 FROM public.resolutions WHERE resolved_by = @id)
              AND NOT EXISTS (SELECT 1 FROM public.service_requests WHERE requester_id = @id)
            """,
            _database,
            transaction);
        command.Parameters.AddWithValue("id", staffId);
        command.Parameters.AddWithValue("name", _staffName);
        command.Parameters.AddWithValue("email", _staffEmail);
        command.Parameters.AddWithValue("role", UserRole.Staff.ToString());
        if (command.ExecuteNonQuery() != 1)
            throw new InvalidOperationException("The exact synthetic staff user could not be safely removed.");
    }

    private static void AddRequestIds(NpgsqlCommand command, int[] requestIds) =>
        command.Parameters.AddWithValue(
            "request_ids",
            NpgsqlDbType.Array | NpgsqlDbType.Integer,
            requestIds);
}
