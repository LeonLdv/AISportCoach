using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AISportCoach.FunctionalTests.Fixtures;

namespace AISportCoach.FunctionalTests;

/// <summary>
/// Functional tests for authentication endpoints.
/// Tests registration, login, token refresh, logout, and user profile retrieval.
/// </summary>
[Collection(AspireCollection.Name)]
public class AuthenticationTests(AspireFixture fixture)
{
    private const string RegisterUrl = "/api/v1/auth/register";
    private const string LoginUrl = "/api/v1/auth/login";
    private const string RefreshUrl = "/api/v1/auth/refresh";
    private const string LogoutUrl = "/api/v1/auth/logout";
    private const string MeUrl = "/api/v1/auth/me";

    #region Register Tests

    [Fact]
    public async Task Register_WithValidData_Returns201AndUserDetails()
    {
        var client = fixture.ApiClient;
        var email = $"test-{Guid.NewGuid()}@example.com";

        var request = new
        {
            Email = email,
            Password = "Test123!",
            DisplayName = "Test User"
        };

        var response = await client.PostAsJsonAsync(RegisterUrl, request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.True(Guid.TryParse(root.GetProperty("userId").GetString(), out _));
        Assert.Equal(email, root.GetProperty("email").GetString());
        Assert.Equal("Test User", root.GetProperty("displayName").GetString());
        Assert.Contains("Registration successful", root.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns400()
    {
        var client = fixture.ApiClient;
        var email = $"duplicate-{Guid.NewGuid()}@example.com";

        var request = new
        {
            Email = email,
            Password = "Test123!",
            DisplayName = "First User"
        };

        // Register first time
        var firstResponse = await client.PostAsJsonAsync(RegisterUrl, request);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // Try to register again with same email
        var secondResponse = await client.PostAsJsonAsync(RegisterUrl, request);
        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Register_WithInvalidEmail_Returns422()
    {
        var client = fixture.ApiClient;

        var request = new
        {
            Email = "not-an-email",
            Password = "Test123!",
            DisplayName = "Test User"
        };

        var response = await client.PostAsJsonAsync(RegisterUrl, request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithShortPassword_Returns400()
    {
        var client = fixture.ApiClient;

        var request = new
        {
            Email = $"test-{Guid.NewGuid()}@example.com",
            Password = "Short1!",
            DisplayName = "Test User"
        };

        var response = await client.PostAsJsonAsync(RegisterUrl, request);

        // Identity validation happens after model validation, returns 400 with error details
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.UnprocessableEntity);
    }

    #endregion

    #region Login Tests

    [Fact]
    public async Task Login_WithValidCredentials_Returns200AndTokens()
    {
        var client = fixture.ApiClient;
        var email = $"login-test-{Guid.NewGuid()}@example.com";
        var password = "Test123!";

        // Register user first
        await RegisterUserAsync(client, email, password, "Login Test User");

        // Login
        var loginRequest = new
        {
            Email = email,
            Password = password
        };

        var response = await client.PostAsJsonAsync(LoginUrl, loginRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // Verify token structure
        var accessToken = root.GetProperty("accessToken").GetString();
        Assert.NotNull(accessToken);
        Assert.NotEmpty(accessToken);
        Assert.Contains(".", accessToken); // JWT has dots

        var refreshToken = root.GetProperty("refreshToken").GetString();
        Assert.NotNull(refreshToken);
        Assert.NotEmpty(refreshToken);

        var expiresAt = root.GetProperty("expiresAt").GetDateTime();
        Assert.True(expiresAt > DateTime.UtcNow);

        // Verify user details
        var user = root.GetProperty("user");
        Assert.Equal(email, user.GetProperty("email").GetString());
        Assert.Equal("Login Test User", user.GetProperty("displayName").GetString());
        Assert.Equal("Free", user.GetProperty("subscriptionTier").GetString());

        var roles = user.GetProperty("roles");
        Assert.Equal(JsonValueKind.Array, roles.ValueKind);
        Assert.Contains("User", roles.EnumerateArray().Select(r => r.GetString()));
    }

    [Fact]
    public async Task Login_WithInvalidEmail_Returns401()
    {
        var client = fixture.ApiClient;

        var loginRequest = new
        {
            Email = "nonexistent@example.com",
            Password = "Test123!"
        };

        var response = await client.PostAsJsonAsync(LoginUrl, loginRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_Returns401()
    {
        var client = fixture.ApiClient;
        var email = $"password-test-{Guid.NewGuid()}@example.com";

        // Register user first
        await RegisterUserAsync(client, email, "Test123!", "Password Test User");

        // Try to login with wrong password
        var loginRequest = new
        {
            Email = email,
            Password = "WrongPassword123!"
        };

        var response = await client.PostAsJsonAsync(LoginUrl, loginRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Refresh Token Tests

    [Fact]
    public async Task RefreshToken_WithValidToken_Returns200AndNewTokens()
    {
        var client = fixture.ApiClient;
        var email = $"refresh-test-{Guid.NewGuid()}@example.com";

        // Register and login to get initial tokens
        await RegisterUserAsync(client, email, "Test123!", "Refresh Test User");
        var (accessToken, refreshToken) = await LoginUserAsync(client, email, "Test123!");

        // Wait a moment to ensure new token will have different timestamp
        await Task.Delay(100);

        // Refresh the token
        var refreshRequest = new
        {
            RefreshToken = refreshToken
        };

        var response = await client.PostAsJsonAsync(RefreshUrl, refreshRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var newAccessToken = root.GetProperty("accessToken").GetString();
        var newRefreshToken = root.GetProperty("refreshToken").GetString();

        // Tokens should be different (token rotation)
        Assert.NotEqual(accessToken, newAccessToken);
        Assert.NotEqual(refreshToken, newRefreshToken);
    }

    [Fact]
    public async Task RefreshToken_WithInvalidToken_Returns401()
    {
        var client = fixture.ApiClient;

        var refreshRequest = new
        {
            RefreshToken = "invalid-token-12345"
        };

        var response = await client.PostAsJsonAsync(RefreshUrl, refreshRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_WithRevokedToken_Returns401()
    {
        var client = fixture.ApiClient;
        var email = $"revoked-test-{Guid.NewGuid()}@example.com";

        // Register and login to get tokens
        await RegisterUserAsync(client, email, "Test123!", "Revoked Test User");
        var (_, refreshToken) = await LoginUserAsync(client, email, "Test123!");

        // Revoke the token by logging out
        var logoutRequest = new { RefreshToken = refreshToken };
        await client.PostAsJsonAsync(LogoutUrl, logoutRequest);

        // Try to use the revoked token
        var refreshRequest = new { RefreshToken = refreshToken };
        var response = await client.PostAsJsonAsync(RefreshUrl, refreshRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Logout Tests

    [Fact]
    public async Task Logout_WithValidToken_Returns204()
    {
        var client = fixture.ApiClient;
        var email = $"logout-test-{Guid.NewGuid()}@example.com";

        // Register and login
        await RegisterUserAsync(client, email, "Test123!", "Logout Test User");
        var (_, refreshToken) = await LoginUserAsync(client, email, "Test123!");

        // Logout
        var logoutRequest = new { RefreshToken = refreshToken };
        var response = await client.PostAsJsonAsync(LogoutUrl, logoutRequest);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Logout_WithInvalidToken_Returns204()
    {
        var client = fixture.ApiClient;

        // Logout with invalid token should still return 204 (idempotent)
        var logoutRequest = new { RefreshToken = "invalid-token" };
        var response = await client.PostAsJsonAsync(LogoutUrl, logoutRequest);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    #endregion

    #region GetCurrentUser Tests

    [Fact]
    public async Task GetCurrentUser_WithValidToken_Returns200AndUserDetails()
    {
        var email = $"me-test-{Guid.NewGuid()}@example.com";
        var displayName = "Me Test User";

        // Get authenticated client (registers and logs in a user)
        var client = await fixture.AuthHelper.GetAuthenticatedClientAsync(email, "Test123!");

        // Note: We need to register with the specific display name first
        // Let's create a fresh user with known details
        var freshClient = fixture.ApiClient;
        var freshEmail = $"me-fresh-{Guid.NewGuid()}@example.com";
        await RegisterUserAsync(freshClient, freshEmail, "Test123!", displayName);
        var (accessToken, _) = await LoginUserAsync(freshClient, freshEmail, "Test123!");

        freshClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        // Get current user
        var response = await freshClient.GetAsync(MeUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.Equal(freshEmail, root.GetProperty("email").GetString());
        Assert.Equal(displayName, root.GetProperty("displayName").GetString());
        Assert.Equal("Free", root.GetProperty("subscriptionTier").GetString());

        var roles = root.GetProperty("roles");
        Assert.Contains("User", roles.EnumerateArray().Select(r => r.GetString()));
    }

    [Fact]
    public async Task GetCurrentUser_WithoutAuthentication_Returns401()
    {
        var client = fixture.ApiClient;

        var response = await client.GetAsync(MeUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUser_WithInvalidToken_Returns401()
    {
        var client = fixture.ApiClient;
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid.token.here");

        var response = await client.GetAsync(MeUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        // Clean up
        client.DefaultRequestHeaders.Authorization = null;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Helper to register a user and return the response.
    /// </summary>
    private static async Task<HttpResponseMessage> RegisterUserAsync(
        HttpClient client,
        string email,
        string password,
        string displayName)
    {
        var request = new
        {
            Email = email,
            Password = password,
            DisplayName = displayName
        };

        var response = await client.PostAsJsonAsync(RegisterUrl, request);
        response.EnsureSuccessStatusCode();
        return response;
    }

    /// <summary>
    /// Helper to login a user and return the access and refresh tokens.
    /// </summary>
    private static async Task<(string AccessToken, string RefreshToken)> LoginUserAsync(
        HttpClient client,
        string email,
        string password)
    {
        var request = new
        {
            Email = email,
            Password = password
        };

        var response = await client.PostAsJsonAsync(LoginUrl, request);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var accessToken = root.GetProperty("accessToken").GetString()!;
        var refreshToken = root.GetProperty("refreshToken").GetString()!;

        return (accessToken, refreshToken);
    }

    #endregion
}
