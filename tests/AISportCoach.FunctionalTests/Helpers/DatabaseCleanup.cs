using Npgsql;

namespace AISportCoach.FunctionalTests.Helpers;

/// <summary>
/// Provides database cleanup utilities for functional tests.
/// </summary>
public static class DatabaseCleanup
{
    /// <summary>
    /// Deletes videos uploaded by ALL test users (including the default test user).
    /// This ensures fresh state for each test run while preserving user accounts.
    /// </summary>
    public static async Task DeleteTestVideosAsync(string connectionString)
    {
        const string deleteQuery = """
            DELETE FROM "VideoUploads"
            WHERE "UserId" IN (
                SELECT "Id" FROM "AspNetUsers"
                WHERE "Email" = 'testuser@test.com'
                   OR "Email" LIKE 'test-%@example.com'
                   OR "Email" LIKE 'pool-user-%@test.example.com'
            );
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(deleteQuery, connection);
        var deleted = await command.ExecuteNonQueryAsync();

        Console.WriteLine($"Deleted {deleted} test videos from database");
    }

    /// <summary>
    /// Deletes coaching reports for ALL test users (including the default test user).
    /// </summary>
    public static async Task DeleteTestReportsAsync(string connectionString)
    {
        const string deleteQuery = """
            DELETE FROM "CoachingReports"
            WHERE "UserId" IN (
                SELECT "Id" FROM "AspNetUsers"
                WHERE "Email" = 'testuser@test.com'
                   OR "Email" LIKE 'test-%@example.com'
                   OR "Email" LIKE 'pool-user-%@test.example.com'
            );
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(deleteQuery, connection);
        var deleted = await command.ExecuteNonQueryAsync();

        Console.WriteLine($"Deleted {deleted} test coaching reports from database");
    }

    /// <summary>
    /// Deletes ONLY auth test users (test-*, login-test-*, etc.).
    /// Preserves the default test user (testuser@test.com).
    /// </summary>
    public static async Task DeleteAuthTestUsersAsync(string connectionString)
    {
        const string deleteQuery = """
            DELETE FROM "AspNetUsers"
            WHERE "Email" LIKE 'test-%@example.com'
               OR "Email" LIKE 'pool-user-%@test.example.com'
               OR "Email" LIKE 'login-test-%'
               OR "Email" LIKE 'duplicate-%'
               OR "Email" LIKE 'refresh-test-%'
               OR "Email" LIKE 'revoked-test-%'
               OR "Email" LIKE 'logout-test-%'
               OR "Email" LIKE 'me-test-%'
               OR "Email" LIKE 'password-test-%'
               OR "Email" LIKE 'emergency-%';
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(deleteQuery, connection);
        var deleted = await command.ExecuteNonQueryAsync();

        Console.WriteLine($"Deleted {deleted} auth test users (preserved testuser@test.com)");
    }

    /// <summary>
    /// Deletes refresh tokens for ALL test users (including the default test user).
    /// Call this before DeleteAuthTestUsersAsync to avoid foreign key constraint violations.
    /// </summary>
    public static async Task DeleteTestRefreshTokensAsync(string connectionString)
    {
        const string deleteQuery = """
            DELETE FROM "RefreshTokens"
            WHERE "UserId" IN (
                SELECT "Id" FROM "AspNetUsers"
                WHERE "Email" = 'testuser@test.com'
                   OR "Email" LIKE 'test-%@example.com'
                   OR "Email" LIKE 'pool-user-%@test.example.com'
            );
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(deleteQuery, connection);
        var deleted = await command.ExecuteNonQueryAsync();

        Console.WriteLine($"Deleted {deleted} test refresh tokens from database");
    }

    /// <summary>
    /// Full cleanup for all test data.
    /// - Videos, reports, and tokens cleaned for ALL users (including default test user)
    /// - Only auth test users are deleted (default test user preserved)
    /// </summary>
    public static async Task CleanupAllTestDataAsync(string connectionString)
    {
        await DeleteTestVideosAsync(connectionString);
        await DeleteTestReportsAsync(connectionString);
        await DeleteTestRefreshTokensAsync(connectionString);
        await DeleteAuthTestUsersAsync(connectionString);
    }
}
