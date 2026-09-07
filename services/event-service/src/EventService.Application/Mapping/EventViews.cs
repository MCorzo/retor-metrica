using EventService.Application.Dtos;
using EventService.Domain.Entities;

namespace EventService.Application.Mapping;

/// <summary>
/// Hand-written projections from the aggregate to read DTOs. Mapping is kept
/// explicit and dependency-free (replaces AutoMapper, which requires a
/// commercial license since v15).
/// </summary>
public static class EventViews
{
    public static ZoneDto ToZone(Zone zone) => new(zone.Id, zone.Name, zone.Price, zone.Capacity);

    public static EventDto ToEvent(Event @event) => new(
        @event.Id,
        @event.Name,
        @event.Date,
        @event.Venue,
        @event.Status,
        @event.Zones.Select(ToZone).ToList());

    public static EventListItemDto ToListItem(Event @event)
    {
        var zones = @event.Zones;
        return new EventListItemDto(
            @event.Id,
            @event.Name,
            @event.Date,
            @event.Venue,
            @event.Status,
            new ZoneSummaryDto(
                PriceFrom: zones.Count > 0 ? zones.Min(z => z.Price) : 0,
                PriceTo: zones.Count > 0 ? zones.Max(z => z.Price) : 0,
                TotalCapacity: zones.Sum(z => z.Capacity),
                ZoneCount: zones.Count));
    }
}
