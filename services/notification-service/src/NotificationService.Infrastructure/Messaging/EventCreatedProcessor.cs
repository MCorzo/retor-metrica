using System.Text.Json;
using EventPlatform.Contracts.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Messaging;

/// <summary>
/// Applies an <c>EventCreated</c> message (previously <c>EventCreatedConsumer</c>):
/// recomputes the payload hash, deduplicates by correlation id (exactly-once,
/// FR-014/SC-003), and persists a durable <c>Pending</c> record for the email
/// scanner. Invoked by <see cref="SqsConsumerHostedService"/> after deserializing
/// the bare SNS payload.
/// </summary>
public sealed class EventCreatedProcessor(
    INotificationRepository repository,
    ILogger<EventCreatedProcessor> logger)
{
    public async Task HandleAsync(EventCreated message, CancellationToken ct)
    {
        var correlationId = message.CorrelationId;

        // Correlate every consumer-side entry with the producer's id (US2).
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
        });

        var existing = await repository.FindByCorrelationIdAsync(correlationId, ct);
        if (existing is not null)
        {
            logger.LogInformation(
                "Duplicate EventCreated skipped (correlation {CorrelationId}, existing {RecordId})",
                correlationId, existing.Id);
            return;
        }

        var payloadHash = PayloadHasher.Hash(message);

        var record = new NotificationRecord(
            eventId: message.EventId,
            eventName: message.EventName,
            eventDate: message.EventDate,
            eventVenue: message.Venue,
            correlationId: correlationId,
            messageTimestamp: message.CreatedAt,
            payloadHash: payloadHash);

        record.SetZoneDetailsJson(JsonSerializer.Serialize(message.Zones));

        await repository.AddAsync(record, ct);
        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Unique correlation index raced a concurrent duplicate — treat as already processed.
            logger.LogInformation(
                "Concurrent duplicate EventCreated rejected (correlation {CorrelationId})", correlationId);
        }

        logger.LogInformation(
            "Consumed EventCreated {EventId} (correlation {CorrelationId}, hash {PayloadHash})",
            message.EventId, correlationId, payloadHash[..Math.Min(12, payloadHash.Length)]);
    }
}