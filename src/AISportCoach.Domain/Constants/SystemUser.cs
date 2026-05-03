namespace AISportCoach.Domain.Constants;

/// <summary>
/// System user used for background jobs, migrations, and operations without an authenticated user.
/// </summary>
public static class SystemUser
{
    public static readonly Guid Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public const string Email = "system@aisportcoach.com";
}
