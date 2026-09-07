using EventService.Domain.Auditing;

namespace EventService.Domain.Entities;

public sealed class Zone : AuditableEntity
{
    private Zone()
    {
    }

    internal Zone(Guid eventId, string name, decimal price, int capacity)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
        {
            throw new ArgumentException("Zone name is required and must not exceed 100 characters.", nameof(name));
        }

        if (price <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Zone price must be greater than zero.");
        }

        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Zone capacity must be greater than zero.");
        }

        Id = Guid.CreateVersion7();
        EventId = eventId;
        Name = name;
        Price = price;
        Capacity = capacity;
    }

    public Guid Id { get; }
    public Guid EventId { get; }
    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public int Capacity { get; private set; }
}
