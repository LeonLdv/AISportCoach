namespace AISportCoach.Domain.Entities;

public class WebAuthnCredential
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public byte[] CredentialId { get; private set; } = [];
    public byte[] PublicKey { get; private set; } = [];
    public long SignatureCounter { get; private set; }
    public string DeviceInfo { get; private set; } = string.Empty; // JSON: browser, OS, device name
    public DateTime RegisteredAt { get; private set; }
    public DateTime LastUsedAt { get; private set; }
    public bool IsActive { get; private set; }

    public ApplicationUser User { get; private set; } = null!;

    public static WebAuthnCredential Create(Guid userId, byte[] credentialId, byte[] publicKey, string deviceInfo)
    {
        var now = DateTime.UtcNow;
        return new WebAuthnCredential
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            CredentialId = credentialId,
            PublicKey = publicKey,
            SignatureCounter = 0,
            DeviceInfo = deviceInfo,
            RegisteredAt = now,
            LastUsedAt = now,
            IsActive = true
        };
    }

    public void UpdateLastUsed(long counter)
    {
        LastUsedAt = DateTime.UtcNow;
        SignatureCounter = counter;
    }

    public void Deactivate() => IsActive = false;
}
