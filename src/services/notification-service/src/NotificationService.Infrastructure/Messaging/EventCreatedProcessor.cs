using System.Text.Json;
using EventPlatform.Contracts.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Messaging;

public sealed class EventCreatedProcessor(
    INotificationRepository repository,
    ILogger<EventCreatedProcessor> logger)
{
    public async Task HandleAsync(EventCreated message, string rawBody, CancellationToken ct)
    {
        var correlationId = message.CorrelationId;

        // Correlate every consumer-side entry with the producer's id.
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
        });

        var existing = await repository.FindByCorrelationIdAsync(correlationId, ct);
        if (existing is not null)
        {
            if (existing.Status == NotificationStatus.Failed && existing.DlqRoutedAt is not null)
            {
                // Broker-tooling replay of the DLQ message:
                // the archived correlation is re-opened for a fresh email cycle.
                existing.RequeueForReplay();
                await repository.SaveChangesAsync(ct);
                logger.LogInformation(
                    "Replay detected for previously failed record {RecordId} — re-queued for email retry (correlation {CorrelationId})",
                    existing.Id, correlationId);
            }
            else
            {
                logger.LogInformation(
                    "Duplicate EventCreated skipped (correlation {CorrelationId}, existing {RecordId}, status {Status})",
                    correlationId, existing.Id, existing.Status);
            }

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
        record.SetOriginalMessageJson(rawBody);

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
