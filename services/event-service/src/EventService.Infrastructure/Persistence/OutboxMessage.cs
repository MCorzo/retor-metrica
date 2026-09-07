using System.Text.Json;
using EventService.Domain.Auditing;

namespace EventService.Infrastructure.Persistence;

public sealed class OutboxMessage : AuditableEntity
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private OutboxMessage()
    {
        TopicArn = null!;
        MessageType = null!;
        Payload = null!;
        Status = OutboxStatus.Pending;
    }

    public OutboxMessage(string topicArn, string messageType, string payload, Guid correlationId)
    {
        Id = Guid.CreateVersion7();
        MessageId = Guid.CreateVersion7();
        TopicArn = topicArn;
        MessageType = messageType;
        Payload = payload;
        CorrelationId = correlationId;
        Status = OutboxStatus.Pending;
        AttemptCount = 0;
        NextTryAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; }

    public Guid MessageId { get; }

    public string TopicArn { get; }

    public string MessageType { get; }

    public string Payload { get; }

    public Guid CorrelationId { get; }

    public string Status { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset NextTryAt { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }
}

public static class OutboxStatus
{
    public const string Pending = "Pending";
    public const string Published = "Published";
}
