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

        Logger.LogInformation("Waiting for {ServiceName} to become healthy...", ResourceNames.ApiService);
        await _app.ResourceNotifications
            .WaitForResourceHealthyAsync(ResourceNames.ApiService)
            .WaitAsync(TimeSpan.FromSeconds(120));
        Logger.LogInformation("{ServiceName} is healthy and ready", ResourceNames.ApiService);

        ApiClient = _app.CreateHttpClient(ResourceNames.ApiService);
        Logger.LogInformation("Test fixture initialized successfully");
    }

    public async Task DisposeAsync()
    {
        Logger?.LogInformation("Disposing test fixture...");
        ApiClient.Dispose();

        if (_app is not null)
        {
            Logger?.LogInformation("Stopping Aspire test application...");
            await _app.DisposeAsync();
            Logger?.LogInformation("Aspire test application stopped");
        }
    }
}
