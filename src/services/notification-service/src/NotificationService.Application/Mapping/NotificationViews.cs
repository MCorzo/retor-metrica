using NotificationService.Application.Dtos;
using NotificationService.Domain.Entities;

namespace NotificationService.Application.Mapping;

public static class NotificationViews
{
    public static NotificationRecordDto ToRecord(NotificationRecord record) => new(
        record.Id,
        record.EventId,
        record.EventName,
        record.EventDate,
        record.EventVenue,
        record.CorrelationId,
        record.MessageTimestamp,
        record.PayloadHash,
        record.Status,
        record.AttemptCount,
        record.NextTryAt,
        record.LastError,
        record.SmtpMessageId);
}
