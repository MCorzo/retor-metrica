using EventPlatform.Contracts.Messages;
using EventService.Application.Abstractions;
using EventService.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace EventService.Infrastructure.Messaging;

/// <summary>
/// Transactional outbox publisher (FR-006). Appends a <c>Pending</c>
/// <see cref="OutboxMessage"/> to the caller's EF Core context — the row commits
/// with the <c>events</c>/<c>zones</c> insert in the same <c>SaveChangesAsync</c>,
/// and <see cref="OutboxRelay"/> ships it to SNS after commit. Never blocks or
/// performs network I/O on the request path.
/// </summary>
public sealed class EventPublisher(
    EventDbContext db,
    AwsBrokerState broker,
    ILogger<EventPublisher> logger) : IEventPublisher
{
    public Task PublishAsync(EventCreated message, CancellationToken ct)
    {
        var topicArn = broker.TopicArn;
        if (string.IsNullOrWhiteSpace(topicArn))
        {
            // BrokerProvisioner runs before the HTTP server binds, so a request
            // can only arrive after the topic ARN is resolved. Defensive guard.
            throw new InvalidOperationException(
                "SNS topic ARN not resolved yet; BrokerProvisioner has not completed provisioning.");
        }

        var payload = System.Text.Json.JsonSerializer.Serialize(message, OutboxMessage.JsonOptions);
        db.Add(new OutboxMessage(topicArn, nameof(EventCreated), payload, message.CorrelationId));

        logger.LogDebug("Queued outbox message {Type} for {Topic}", nameof(EventCreated), topicArn);
        return Task.CompletedTask;
    }
}