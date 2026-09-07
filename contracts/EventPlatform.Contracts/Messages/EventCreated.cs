using System.Text.Json.Serialization;

namespace EventPlatform.Contracts.Messages;

/// <summary>
/// Contract: <c>EventCreated</c> v1 — published by EventService to SNS on
/// successful creation, consumed by NotificationService. Contract-first,
/// additive-only (see <c>contracts/event-created.v1.md</c>).
/// The wire payload is camelCase JSON (System.Text.Json default).
/// </summary>
public sealed class EventCreated
{
    public Guid EventId { get; init; }
    public string EventName { get; init; } = string.Empty;
    public DateTimeOffset EventDate { get; init; }
    public string Venue { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid OwnerId { get; init; }

    [JsonPropertyName("zones")]
    public IReadOnlyList<EventZone> Zones { get; init; } = [];

    /// <summary>Unique per event creation — consumer dedup key (FR-014).</summary>
    public Guid CorrelationId { get; init; }
    public int SchemaVersion { get; init; } = 1;
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class EventZone
{
    public string Name { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public int Capacity { get; init; }
}
