namespace AISportCoach.Application.Options;

public record JwtOptions
{
    public string SecretKey { get; init; } = "";
    public string Issuer { get; init; } = "AISportCoach.API";
    public string Audience { get; init; } = "AISportCoach.WebApp";
    public int AccessTokenExpiryMinutes { get; init; } = 15;
    public int RefreshTokenExpiryDays { get; init; } = 7;
}
