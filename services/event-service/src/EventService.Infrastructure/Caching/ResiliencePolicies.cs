using Polly;
using Polly.Retry;
using StackExchange.Redis;

namespace EventService.Infrastructure.Caching;

/// <summary>
/// Polly v8 resilience policies for outbound dependencies (constitution
/// Principle IV): retry with exponential backoff + jitter, bounded attempts.
/// </summary>
public static class ResiliencePolicies
{
    public static ResiliencePipeline RedisListPipeline() =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<RedisConnectionException>()
                    .Handle<RedisServerException>()
                    .Handle<RedisTimeoutException>(),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(150),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
            })
            .AddTimeout(TimeSpan.FromSeconds(3))
            .Build();
}
