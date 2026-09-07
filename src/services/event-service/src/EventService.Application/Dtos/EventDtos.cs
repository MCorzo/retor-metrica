namespace EventService.Application.Dtos;

public sealed record CreateEventRequest(
    string Name,
    DateTimeOffset Date,
    string Venue,
    string Status,
    List<CreateZoneRequest> Zones);

public sealed record CreateZoneRequest(string Name, decimal Price, int Capacity);

public sealed record ZoneDto(Guid Id, string Name, decimal Price, int Capacity);

public sealed record EventDto(
    Guid Id,
    string Name,
    DateTimeOffset Date,
    string Venue,
    string Status,
    IReadOnlyList<ZoneDto> Zones);

public sealed record EventListItemDto(
    Guid Id,
    string Name,
    DateTimeOffset Date,
    string Venue,
    string Status,
    ZoneSummaryDto ZoneSummary);

public sealed record ZoneSummaryDto(
    decimal PriceFrom,
    decimal PriceTo,
    int TotalCapacity,
    int ZoneCount);

public sealed record EventListResponse(IReadOnlyList<EventListItemDto> Items);
