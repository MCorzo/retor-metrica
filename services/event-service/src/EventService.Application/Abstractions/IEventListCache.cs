using EventService.Application.Dtos;

namespace EventService.Application.Abstractions;

/// <summary>
/// Role-aware cache-aside store for event list projections (FR-007).
/// Cache is never a source of truth — the DB is authoritative.
/// </summary>
public interface IEventListCache
{
    Task<EventListItemDto[]?> GetAsync(string key, CancellationToken ct);

    Task SetAsync(string key, IReadOnlyList<EventListItemDto> items, TimeSpan ttl, CancellationToken ct);

    Task InvalidateAsync(IEnumerable<string> keys, CancellationToken ct);
}

/// <summary>Versioned, role-aware Redis key composition (data-model.md / research.md).</summary>
public static class EventListCacheKeys
{
    public const string VersionPrefix = "events:v1";

    public static string AdminList() => $"{VersionPrefix}:admin:list";

    public static string OwnerList(Guid ownerId) => $"{VersionPrefix}:owner:{ownerId}:list";

    public static string ClientList() => $"{VersionPrefix}:client:list";
}
