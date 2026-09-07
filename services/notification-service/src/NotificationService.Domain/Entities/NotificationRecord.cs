using NotificationService.Domain.Auditing;

namespace NotificationService.Domain.Entities;

public sealed class NotificationRecord : AuditableEntity
{
    private NotificationRecord()
    {
        EventName = string.Empty;
        EventVenue = string.Empty;
        PayloadHash = string.Empty;
    }

    public NotificationRecord(
        Guid eventId,
        string eventName,
        DateTimeOffset eventDate,
        string eventVenue,
        Guid correlationId,
        DateTimeOffset messageTimestamp,
        string payloadHash)
    {
        Id = Guid.CreateVersion7();
        EventId = eventId;
        EventName = eventName;
        EventDate = eventDate;
        EventVenue = eventVenue;
        CorrelationId = correlationId;
        MessageTimestamp = messageTimestamp;
        PayloadHash = payloadHash;
        Status = NotificationStatus.Pending;
        AttemptCount = 0;
        NextTryAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; }
    public Guid EventId { get; }
    public string EventName { get; }
    public DateTimeOffset EventDate { get; }
    public string EventVenue { get; }
    public Guid CorrelationId { get; }
    public DateTimeOffset MessageTimestamp { get; }
    public string PayloadHash { get; }
    public string Status { get; private set; } = NotificationStatus.Pending;
    public int AttemptCount { get; private set; }
    public DateTimeOffset? NextTryAt { get; private set; }
    public string? LastError { get; private set; }
    public string? SmtpMessageId { get; private set; }

    /// <summary>JSON snapshot of the message zones (name/price/capacity) so the email summary survives schema change (FR-015 / SC-005).</summary>
    public string? ZoneDetailsJson { get; private set; }

    public void SetZoneDetailsJson(string json) => ZoneDetailsJson = json;

    public void MarkSent(string smtpMessageId)
    {
        Status = NotificationStatus.Sent;
        SmtpMessageId = smtpMessageId;
        NextTryAt = null;
        LastError = null;
    }

    public void RecordFailure(string sanitizedReason, TimeSpan retryIn)
    {
        AttemptCount += 1;
        LastError = sanitizedReason;
        Status = NotificationStatus.Failed;
        NextTryAt = DateTimeOffset.UtcNow.Add(retryIn);
    }

    /// <summary>
    /// Transient send failure (connection/protocol/timeout/4xx): the record stays
    /// <c>Pending</c> and gets re-scanned after <paramref name="retryIn"/>. Each
    /// attempt bumps <c>attempt_count</c> (data-model.md: "email send attempts").
    /// </summary>
    public void RecordTransientFailure(string sanitizedReason, TimeSpan retryIn)
    {
        AttemptCount += 1;
        LastError = sanitizedReason;
        Status = NotificationStatus.Pending;
        NextTryAt = DateTimeOffset.UtcNow.Add(retryIn);
    }
}

public static class NotificationStatus
{
    public const string Pending = "Pending";
    public const string Sent = "Sent";
    public const string Failed = "Failed";
}
