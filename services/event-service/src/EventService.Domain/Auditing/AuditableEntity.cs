namespace EventService.Domain.Auditing;

/// <summary>
/// Base class enforcing the platform's mandatory audit fields on every record
/// (constitution v1.8.0). Values are maintained automatically by the audit
/// <c>SaveChanges</c> interceptor — never by hand.
/// </summary>
public abstract class AuditableEntity
{
    public Guid UserRecordCreation { get; set; }
    public DateTimeOffset DateRecordCreation { get; set; }
    public Guid? UserRecordEdit { get; set; }
    public DateTimeOffset? DateRecordEdit { get; set; }
}
