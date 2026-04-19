namespace AISportCoach.Domain.Entities;

public abstract class AuditableEntity
{
    public DateTime CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTime? LastModifiedAt { get; private set; }
    public Guid? LastModifiedBy { get; private set; }

    public void SetCreated(Guid userId, DateTime utcNow)
    {
        CreatedAt = utcNow;
        CreatedBy = userId;
    }

    public void SetModified(Guid userId, DateTime utcNow)
    {
        LastModifiedAt = utcNow;
        LastModifiedBy = userId;
    }
}
