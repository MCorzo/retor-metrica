using System.Text.Json;
using EventService.Application.Abstractions;
using EventService.Application.Dtos;
using Polly;
using Polly.Retry;
using StackExchange.Redis;

namespace EventService.Infrastructure.Caching;

public sealed class EventListCache(IConnectionMultiplexer redis) : IEventListCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ResiliencePipeline _pipeline = ResiliencePolicies.RedisListPipeline();

    public async Task<EventListItemDto[]?> GetAsync(string key, CancellationToken ct)
    {
        return await _pipeline.ExecuteAsync(async token =>
        {
            var value = await redis.GetDatabase().StringGetAsync(key).ConfigureAwait(false);
            if (value.IsNullOrEmpty)
            {
                return null;
            }

            return JsonSerializer.Deserialize<EventListItemDto[]>(value.ToString()!, JsonOptions);
        }, ct).ConfigureAwait(false);
    }

    public async Task SetAsync(string key, IReadOnlyList<EventListItemDto> items, TimeSpan ttl, CancellationToken ct)
    {
        await _pipeline.ExecuteAsync(async token =>
        {
            var json = JsonSerializer.Serialize(items, JsonOptions);
            await redis.GetDatabase().StringSetAsync(key, json, ttl).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    public async Task InvalidateAsync(IEnumerable<string> keys, CancellationToken ct)
    {
        await _pipeline.ExecuteAsync(async token =>
        {
            var db = redis.GetDatabase();
            foreach (var key in keys)
            {
                await db.KeyDeleteAsync(key).ConfigureAwait(false);
            }
        }, ct).ConfigureAwait(false);
    }
}
