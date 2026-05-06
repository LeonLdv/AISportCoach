using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AISportCoach.ServiceDefaults;
using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace AISportCoach.FunctionalTests.Helpers;

public class TestAuthHelper(DistributedApplication app)
{
    private readonly DistributedApplication _app = app;

    /// <summary>
    /// Creates a test user via the registration endpoint and returns their credentials and access token.
    /// </summary>
    public async Task<(string Email, string AccessToken)> CreateTestUserAndLoginAsync(
        string? email = null,
        string password = "Test123!",
        string? displayName = null)
    {
        email ??= $"test-{Guid.NewGuid()}@example.com";
        displayName ??= email.Split('@')[0];

        var client = CreateHttpClient();

        // Register user
        var registerRequest = new
        {
            Email = email,
            Password = password,
            DisplayName = displayName
        };

        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

        // If user already exists (409), that's okay for tests - just try to login
        if (!registerResponse.IsSuccessStatusCode && registerResponse.StatusCode != System.Net.HttpStatusCode.Conflict)
        {
            var errorBody = await registerResponse.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Failed to register test user. Status: {registerResponse.StatusCode}, Body: {errorBody}");
        }

        // Login to get access token
        var loginRequest = new
        {
            Email = email,
            Password = password
        };

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        loginResponse.EnsureSuccessStatusCode();

        var loginBody = await loginResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(loginBody);
        var accessToken = doc.RootElement.GetProperty("accessToken").GetString()
            ?? throw new InvalidOperationException("Access token not found in login response");

        return (email, accessToken);
    }

    /// <summary>
    /// Gets an authenticated HttpClient with a valid Bearer token.
    /// Creates a new test user if needed.
    /// </summary>
    public async Task<HttpClient> GetAuthenticatedClientAsync(
        string? email = null,
        string password = "Test123!")
    {
        System.Diagnostics.Debug.WriteLine("GetAuthenticatedClientAsync: Starting");

        var (_, token) = await CreateTestUserAndLoginAsync(email, password);
        System.Diagnostics.Debug.WriteLine($"GetAuthenticatedClientAsync: Got token: {token[..20]}...");

        var client = CreateHttpClient();
        System.Diagnostics.Debug.WriteLine($"GetAuthenticatedClientAsync: Client created with BaseAddress: {client.BaseAddress}");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    /// <summary>
    /// Gets an authenticated HttpClient using the default test user.
    /// Use this for all non-auth endpoint tests (videos, analysis, token management).
    /// The default user is created once during fixture initialization and reused across all tests.
    /// </summary>
    public async Task<HttpClient> GetDefaultAuthenticatedClientAsync()
    {
        var user = TestUsers.Default;

        // Login to get access token (user already created during fixture initialization)
        var loginRequest = new
        {
            Email = user.Email,
            Password = user.Password
        };

        var client = CreateHttpClient();
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        loginResponse.EnsureSuccessStatusCode();

        var loginBody = await loginResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(loginBody);
        var accessToken = doc.RootElement.GetProperty("accessToken").GetString()
            ?? throw new InvalidOperationException("Access token not found in login response");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return client;
    }

    /// <summary>
    /// Creates an HttpClient for the API service.
    /// Uses HTTP in test environment (Aspire doesn't expose HTTPS endpoints by default).
    /// Timeout is extended to 10 minutes so tests that upload large videos through
    /// this client are not cut short by the default 100s HttpClient.Timeout.
    /// </summary>
    private HttpClient CreateHttpClient()
    {
        var client = _app.CreateHttpClient(ResourceNames.ApiService);
        client.Timeout = TimeSpan.FromMinutes(10);
        return client;
    }
}
