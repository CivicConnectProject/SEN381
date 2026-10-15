using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CivicConnectMvc.Tests;

internal static class ApiHttpTests
{
    private const string IsolatedConnectionString =
        "Host=192.0.2.1;Port=5432;Database=civicconnect_http_test;Timeout=1;Command Timeout=1;Pooling=false";

    public static void Invalid_Create_Returns_ValidationProblem()
    {
        const string connectionStringEnvironmentVariable = "ConnectionStrings__CivicConnectDb";
        var previousConnectionString = Environment.GetEnvironmentVariable(connectionStringEnvironmentVariable);
        Environment.SetEnvironmentVariable(connectionStringEnvironmentVariable, IsolatedConnectionString);

        try
        {
            using var factory = new IsolatedApplicationFactory();
            using var client = factory.CreateClient();

            var configuredConnectionString = factory.Services
                .GetRequiredService<IConfiguration>()
                .GetConnectionString("CivicConnectDb");

            if (string.IsNullOrWhiteSpace(configuredConnectionString))
                throw new InvalidOperationException("The isolated HTTP-test database configuration is missing.");

            var configuredDatabase = new NpgsqlConnectionStringBuilder(configuredConnectionString);
            if (configuredDatabase.Database != "civicconnect_http_test" ||
                configuredDatabase.Host != "192.0.2.1")
            {
                throw new InvalidOperationException("The HTTP test host is not using its isolated, non-routable database configuration.");
            }

            using var response = client.PostAsJsonAsync(
                    "/api/servicerequests",
                    new { title = "", description = "", categoryId = 0 })
                .GetAwaiter()
                .GetResult();

            if (response.StatusCode != HttpStatusCode.BadRequest)
                throw new InvalidOperationException($"Expected HTTP 400, received {(int)response.StatusCode}.");

            if (response.Content.Headers.ContentType?.MediaType != "application/problem+json")
                throw new InvalidOperationException("Expected an application/problem+json validation response.");

            using var document = JsonDocument.Parse(
                response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            var root = document.RootElement;

            if (!root.TryGetProperty("status", out var status) || status.GetInt32() != 400)
                throw new InvalidOperationException("The validation problem response did not contain status 400.");

            if (!root.TryGetProperty("errors", out var errors) ||
                !errors.TryGetProperty("Title", out _) ||
                !errors.TryGetProperty("Description", out _) ||
                !errors.TryGetProperty("CategoryId", out _))
            {
                throw new InvalidOperationException("The validation problem response omitted expected model validation errors.");
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(connectionStringEnvironmentVariable, previousConnectionString);
        }
    }

    private sealed class IsolatedApplicationFactory : WebApplicationFactory<global::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
        }
    }
}
