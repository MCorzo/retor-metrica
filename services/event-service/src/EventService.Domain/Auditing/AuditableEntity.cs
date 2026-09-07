namespace EventService.Domain.Auditing;

public abstract class AuditableEntity
{
    public Guid UserRecordCreation { get; set; }
    public DateTimeOffset DateRecordCreation { get; set; }
    public Guid? UserRecordEdit { get; set; }
    public DateTimeOffset? DateRecordEdit { get; set; }
}
