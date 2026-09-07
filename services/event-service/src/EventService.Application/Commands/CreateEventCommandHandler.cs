using EventPlatform.Contracts.Messages;
using EventService.Application.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;
using Event = EventService.Domain.Entities.Event;

namespace EventService.Application.Commands;

public sealed class CreateEventCommandHandler(
    IEventRepository repository,
    IEventListCache cache,
    IEventPublisher eventPublisher,
    ILogger<CreateEventCommandHandler> logger) : IRequestHandler<CreateEventCommand, CreateEventCommandResult>
{
    public async Task<CreateEventCommandResult> Handle(CreateEventCommand request, CancellationToken ct)
    {
        var @event = new Event(request.Name, request.Date, request.Venue, request.Status, request.OwnerId);
        foreach (var zone in request.Zones)
        {
            @event.AddZone(zone.Name, zone.Price, zone.Capacity);
        }

        var now = DateTimeOffset.UtcNow;
        var correlationId = Guid.CreateVersion7();

        await repository.AddAsync(@event, ct);
        await eventPublisher.PublishAsync(new EventCreated
        {
            EventId = @event.Id,
            EventName = @event.Name,
            EventDate = @event.Date,
            Venue = @event.Venue,
            Status = @event.Status,
            OwnerId = @event.OwnerId,
            Zones = @event.Zones.Select(z => new EventZone
            {
                Name = z.Name,
                Price = z.Price,
                Capacity = z.Capacity,
            }).ToList(),
            CorrelationId = correlationId,
            SchemaVersion = 1,
            CreatedAt = now,
        }, ct);

        // Single transaction: event + zones + outbox row commit atomically (FR-001/FR-002/FR-005/FR-006).
        await repository.SaveChangesAsync(ct);

        await InvalidateCachesAsync(@event, ct);

        logger.LogInformation("Event {EventId} created with {ZoneCount} zones (correlation {CorrelationId})",
            @event.Id, @event.Zones.Count, correlationId);

        return new CreateEventCommandResult(@event.Id);
    }

    /// <summary>
    /// Delete-on-invalidate (FR-007): a freshly created event must appear on the
    /// next read. Fail-open — a stale cache row self-expires via TTL and the DB
    /// remains authoritative, so invalidation errors never fail the request
    /// that already committed.
    /// </summary>
    private async Task InvalidateCachesAsync(Event @event, CancellationToken ct)
    {
        var keys = new List<string>
        {
            EventListCacheKeys.AdminList(),
            EventListCacheKeys.OwnerList(@event.OwnerId),
        };

        if (@event.Status == "published")
        {
            keys.Add(EventListCacheKeys.ClientList());
        }

        try
        {
            await cache.InvalidateAsync(keys, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Cache invalidation failed after event {EventId} was committed: {Reason}",
                @event.Id, ex.Message);
        }
    }
}
