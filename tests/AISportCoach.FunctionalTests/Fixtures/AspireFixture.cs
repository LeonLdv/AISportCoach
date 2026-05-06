using System.Net.Http.Json;
using AISportCoach.FunctionalTests.Helpers;
using AISportCoach.ServiceDefaults;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AISportCoach.FunctionalTests.Fixtures;

public class AspireFixture : IAsyncLifetime
{
    private DistributedApplication? _app;

    public HttpClient ApiClient { get; private set; } = null!;
    public ILogger Logger { get; private set; } = null!;
    public TestAuthHelper AuthHelper { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.AISportCoach_AppHost>(
            [
                "--Parameters:postgres-password", "postgres"
            ]);

        _app = await builder.BuildAsync();

        // Create logger for test fixture
        var loggerFactory = _app.Services.GetRequiredService<ILoggerFactory>();
        Logger = loggerFactory.CreateLogger<AspireFixture>();

        Logger.LogInformation("Starting Aspire test application...");
        await _app.StartAsync();
        Logger.LogInformation("Aspire test application started");

        // Initialize default test user
        await InitializeDefaultTestUserAsync();

        Logger.LogInformation("Waiting for {ServiceName} to become healthy...", ResourceNames.ApiService);
        await _app.ResourceNotifications
            .WaitForResourceHealthyAsync(ResourceNames.ApiService)
            .WaitAsync(TimeSpan.FromSeconds(120));
        Logger.LogInformation("{ServiceName} is healthy and ready", ResourceNames.ApiService);

        ApiClient = CreateHttpClient();
        AuthHelper = new TestAuthHelper(_app);
        Logger.LogInformation("Test fixture initialized successfully");
    }

    public async Task DisposeAsync()
    {
        Logger?.LogInformation("Disposing test fixture...");
        ApiClient.Dispose();

        // Cleanup test data based on environment variable
        var shouldCleanup = Environment.GetEnvironmentVariable("CLEANUP_TEST_USERS")?.ToLower() == "true";
        if (shouldCleanup)
        {
            Logger?.LogInformation("Cleaning up test data...");
            await CleanupTestDataAsync();
        }
        else
        {
            Logger?.LogInformation("Skipping test data cleanup (set CLEANUP_TEST_USERS=true to enable)");
        }

        if (_app is not null)
        {
            Logger?.LogInformation("Stopping Aspire test application...");
            await _app.DisposeAsync();
            Logger?.LogInformation("Aspire test application stopped");
        }
    }

    /// <summary>
    /// Initializes the default test user account.
    /// This user is created once and reused across all non-auth tests.
    /// </summary>
    private async Task InitializeDefaultTestUserAsync()
    {
        Logger?.LogInformation("Initializing default test user...");

        var client = CreateHttpClient();
        var user = TestUsers.Default;

        try
        {
            var request = new
            {
                Email = user.Email,
                Password = user.Password,
                DisplayName = user.DisplayName
            };

            var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

            if (response.StatusCode == System.Net.HttpStatusCode.Created)
            {
                Logger?.LogInformation("Created default test user: {Email}", user.Email);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                Logger?.LogDebug("Default test user already exists: {Email}", user.Email);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Logger?.LogWarning("Failed to create default test user {Email}: {StatusCode} - {Error}",
                    user.Email, response.StatusCode, error);
            }
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "Error initializing default test user {Email}", user.Email);
        }

        Logger?.LogInformation("Default test user initialization complete");
    }

    /// <summary>
    /// Cleans up test users and related data from the database.
    /// Call this manually at the end of test runs if needed.
    /// </summary>
    public async Task CleanupTestDataAsync()
    {
        try
        {
            // Get the connection string from the Aspire app
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__tenniscoach")
                ?? "Host=localhost;Database=tenniscoach;Username=postgres;Password=postgres";

            Logger?.LogInformation("Cleaning up test data from database...");
            await DatabaseCleanup.CleanupAllTestDataAsync(connectionString);
            Logger?.LogInformation("Test data cleanup completed");
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "Failed to cleanup test data");
        }
    }

    /// <summary>
    /// Creates an HttpClient for the API service.
    /// Uses HTTP in test environment (Aspire doesn't expose HTTPS endpoints by default).
    /// </summary>
    /// <param name="timeout">Optional timeout override. Default is 10 minutes for large file uploads.</param>
    private HttpClient CreateHttpClient(TimeSpan? timeout = null)
    {
        var client = _app!.CreateHttpClient(ResourceNames.ApiService);
        client.Timeout = timeout ?? TimeSpan.FromMinutes(10); // Long timeout for video uploads
        return client;
    }
}
