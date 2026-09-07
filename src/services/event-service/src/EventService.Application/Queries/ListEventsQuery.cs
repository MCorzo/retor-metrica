using EventService.Application.Abstractions;
using EventService.Application.Dtos;
using EventService.Application.Mapping;
using MediatR;

namespace EventService.Application.Queries;

public sealed record ListEventsQuery(
    EventScope Scope,
    Guid? OwnerId,
    string? Status,
    DateTimeOffset? After,
    int Limit) : IRequest<ListEventsResult>;

public sealed record ListEventsResult(EventListResponse Response, bool ServedFromCache);

public sealed class ListEventsQueryHandler(
    IEventRepository repository,
    IEventListCache cache) : IRequestHandler<ListEventsQuery, ListEventsResult>
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(100);

    public async Task<ListEventsResult> Handle(ListEventsQuery request, CancellationToken ct)
    {
        var cacheKey = ResolveCacheKey(request.Scope, request.OwnerId);

        var cached = await cache.GetAsync(cacheKey, ct);
        if (cached is not null)
        {
            return new ListEventsResult(new EventListResponse(cached), ServedFromCache: true);
        }

        var events = await repository.QueryAsync(request.Scope, request.OwnerId, request.Status, request.After, request.Limit, ct);
        var items = events.Select(EventViews.ToListItem).ToList();

        var response = new EventListResponse(items);
        await cache.SetAsync(cacheKey, items, CacheTtl, ct);

        return new ListEventsResult(response, ServedFromCache: false);
    }

    private static string ResolveCacheKey(EventScope scope, Guid? ownerId) => scope switch
    {
        EventScope.Admin => EventListCacheKeys.AdminList(),
        EventScope.Client => EventListCacheKeys.ClientList(),
        EventScope.Organizer => EventListCacheKeys.OwnerList(ownerId ?? Guid.Empty),
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null),
    };
}
