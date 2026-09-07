using EventService.Domain.Entities;

namespace EventService.Application.Abstractions;

public enum EventScope
{
    Admin,
    Organizer,
    Client,
}

/// <summary>Persistence port for events/zones. Implemented by EventService.Infrastructure.</summary>
public interface IEventRepository
{
    Task AddAsync(Event entity, CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct);

    Task<Event?> GetByIdWithZonesAsync(Guid id, CancellationToken ct);

    /// <summary>Returns events filtered by role scope + optional query params (status, after, limit).</summary>
    Task<IReadOnlyList<Event>> QueryAsync(
        EventScope scope,
        Guid? ownerId,
        string? status,
        DateTimeOffset? after,
        int limit,
        CancellationToken ct);
}
