using System.Text.Json.Serialization;

namespace EventPlatform.Contracts.Messages;

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
