using AISportCoach.ServiceDefaults;
using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace AISportCoach.FunctionalTests.Fixtures;

public class AspireFixture : IAsyncLifetime
{
    private DistributedApplication? _app;

    public HttpClient ApiClient { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.AISportCoach_AppHost>(
            [
                "--Parameters:postgres-password", "postgres"
            ]);

        _app = await builder.BuildAsync();
        await _app.StartAsync();

        await _app.ResourceNotifications
            .WaitForResourceHealthyAsync(ResourceNames.ApiService)
            .WaitAsync(TimeSpan.FromSeconds(120));

        ApiClient = _app.CreateHttpClient(ResourceNames.ApiService);
    }

    public async Task DisposeAsync()
    {
        ApiClient.Dispose();

        if (_app is not null)
            await _app.DisposeAsync();
    }
}
