using EventService.Domain.Auditing;

namespace EventService.Domain.Entities;

public sealed class Event : AuditableEntity
{
    private readonly List<Zone> _zones = [];

    private Event()
    {
    }

    public Event(string name, DateTimeOffset date, string venue, string status, Guid ownerId)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
        {
            throw new ArgumentException("Event name is required and must not exceed 200 characters.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(venue) || venue.Length > 300)
        {
            throw new ArgumentException("Event venue is required and must not exceed 300 characters.", nameof(venue));
        }

        if (!IsValidStatus(status))
        {
            throw new ArgumentException("Event status must be 'draft' or 'published'.", nameof(status));
        }

        Id = Guid.CreateVersion7();
        Name = name;
        Date = date;
        Venue = venue;
        Status = status;
        OwnerId = ownerId;
    }

    public Guid Id { get; }
    public string Name { get; private set; } = string.Empty;
    public DateTimeOffset Date { get; private set; }
    public string Venue { get; private set; } = string.Empty;

    public string Status { get; private set; } = string.Empty;

    public Guid OwnerId { get; private set; }

    public IReadOnlyList<Zone> Zones => _zones;

    public void AddZone(string name, decimal price, int capacity) => _zones.Add(new Zone(Id, name, price, capacity));

    public static bool IsValidStatus(string status) => status is "draft" or "published";
}
