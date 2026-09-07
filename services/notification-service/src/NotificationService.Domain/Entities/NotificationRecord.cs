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

    public string? ZoneDetailsJson { get; private set; }

    public void SetZoneDetailsJson(string json) => ZoneDetailsJson = json;

    public string? OriginalMessageJson { get; private set; }

    public string? DlqMessageId { get; private set; }

    public DateTimeOffset? DlqRoutedAt { get; private set; }

    public void SetOriginalMessageJson(string json) => OriginalMessageJson = json;

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

    public void MarkTerminal(string sanitizedReason)
    {
        AttemptCount += 1;
        LastError = sanitizedReason;
        Status = NotificationStatus.Failed;
        NextTryAt = DateTimeOffset.UtcNow;
    }

    public void MarkDlqRouted(string dlqMessageId)
    {
        DlqMessageId = dlqMessageId;
        DlqRoutedAt = DateTimeOffset.UtcNow;
        NextTryAt = null;
    }

    public void MarkDlqRoutePending(TimeSpan retryIn)
    {
        NextTryAt = DateTimeOffset.UtcNow.Add(retryIn);
    }

    public void RequeueForReplay()
    {
        Status = NotificationStatus.Pending;
        AttemptCount = 0;
        NextTryAt = DateTimeOffset.UtcNow;
    }

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
