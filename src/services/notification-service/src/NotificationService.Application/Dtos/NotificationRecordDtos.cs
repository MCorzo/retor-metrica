namespace NotificationService.Application.Dtos;

public sealed record NotificationRecordDto(
    Guid Id,
    Guid EventId,
    string EventName,
    DateTimeOffset EventDate,
    string EventVenue,
    Guid CorrelationId,
    DateTimeOffset MessageTimestamp,
    string PayloadHash,
    string Status,
    int AttemptCount,
    DateTimeOffset? NextTryAt,
    string? LastError,
    string? SmtpMessageId);

public sealed record NotificationListResponse(IReadOnlyList<NotificationRecordDto> Items);
