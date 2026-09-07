using EventService.Application.Dtos;

namespace EventService.Application.Abstractions;

public interface IEventListCache
{
    Task<EventListItemDto[]?> GetAsync(string key, CancellationToken ct);

    Task SetAsync(string key, IReadOnlyList<EventListItemDto> items, TimeSpan ttl, CancellationToken ct);

    Task InvalidateAsync(IEnumerable<string> keys, CancellationToken ct);
}

public static class EventListCacheKeys
{
    public const string VersionPrefix = "events:v1";

    public static string AdminList() => $"{VersionPrefix}:admin:list";

    public static string OwnerList(Guid ownerId) => $"{VersionPrefix}:owner:{ownerId}:list";

    public static string ClientList() => $"{VersionPrefix}:client:list";
}
