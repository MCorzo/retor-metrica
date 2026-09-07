using EventService.Domain.Entities;

namespace EventService.Application.Abstractions;

public enum EventScope
{
    Admin,
    Organizer,
    Client,
}

public interface IEventRepository
{
    Task AddAsync(Event entity, CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct);

    Task<Event?> GetByIdWithZonesAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<Event>> QueryAsync(
        EventScope scope,
        Guid? ownerId,
        string? status,
        DateTimeOffset? after,
        int limit,
        CancellationToken ct);
}
