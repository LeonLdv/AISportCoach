namespace AISportCoach.FunctionalTests.Helpers;

/// <summary>
/// Pre-defined test user for functional testing.
/// This user is created once during fixture initialization and reused across all non-auth tests.
/// </summary>
public static class TestUsers
{
    /// <summary>
    /// Default test user for all general endpoint testing (videos, analysis, etc.).
    /// Use GetDefaultAuthenticatedClientAsync() to get an authenticated client for this user.
    /// </summary>
    public static readonly TestUser Default = new(
        Email: "testuser@test.com",
        Password: "Test123!",
        DisplayName: "Test User ");

    public record TestUser(string Email, string Password, string DisplayName);
}
