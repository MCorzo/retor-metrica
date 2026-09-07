using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using EventService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventService.Infrastructure.Messaging;

/// <summary>
/// Background relay (US1, FR-006): polls due <c>Pending</c> outbox rows every
/// ~10 s, claims each row with an atomic lease the same instance cannot
/// re-process, publishes the raw payload to SNS, and marks it <c>Published</c>.
/// Failures are recorded with backoff and retried. A crashed relay is taken
/// over by the lease expiring (<c>next_try_at</c>), so no message is lost.
/// </summary>
public sealed class OutboxRelay(
    IServiceScopeFactory scopeFactory,
    IAmazonSimpleNotificationService sns,
    AwsBrokerState broker,
    ILogger<OutboxRelay> logger) : BackgroundService
{
    private static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ClaimLease = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Outbox relay batch failed; retrying in {Delay}", PollDelay);
            }

            await Task.Delay(PollDelay, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventDbContext>();

        var now = DateTimeOffset.UtcNow;
        var due = await db.OutboxMessages
            .Where(m => m.Status == OutboxStatus.Pending && m.NextTryAt <= now)
            .OrderBy(m => m.NextTryAt)
            .Take(BatchSize)
            .Select(m => new { m.Id, m.NextTryAt, m.TopicArn, m.Payload })
            .ToListAsync(ct);

        foreach (var row in due)
        {
            if (!await TryClaimAsync(db, row.Id, row.NextTryAt, ct))
            {
                continue;
            }

            var topicArn = broker.TopicArn ?? row.TopicArn;
            try
            {
                await sns.PublishAsync(topicArn, row.Payload, ct);
                await MarkPublishedAsync(db, row.Id, ct);
                logger.LogInformation("Published outbox row {Id} to SNS topic {Topic}", row.Id, topicArn);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (AmazonServiceException ex)
            {
                var reason = Sanitize(ex);
                await RecordFailureAsync(db, row.Id, reason, ct);
                logger.LogWarning("SNS publish failed for outbox row {Id}: {Reason}", row.Id, reason);
            }
        }
    }

    /// <summary>
    /// Race-safe claim without a lock table: pushes <c>next_try_at</c> forward
    /// (lease) only if the row is still <c>Pending</c> and unchanged since read.
    /// Returns false when another process already owns the row.
    /// </summary>
    private static async Task<bool> TryClaimAsync(EventDbContext db, Guid id, DateTimeOffset previousNextTryAt, CancellationToken ct)
    {
        var claimed = await db.OutboxMessages
            .Where(m => m.Id == id && m.Status == OutboxStatus.Pending && m.NextTryAt == previousNextTryAt)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.NextTryAt, DateTimeOffset.UtcNow.Add(ClaimLease)), ct);
        return claimed == 1;
    }

    private static async Task MarkPublishedAsync(EventDbContext db, Guid id, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        await db.OutboxMessages
            .Where(m => m.Id == id && m.Status == OutboxStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, OutboxStatus.Published)
                .SetProperty(m => m.PublishedAt, now)
                .SetProperty(m => m.NextTryAt, now)
                .SetProperty(m => m.LastError, (string?)null)
                .SetProperty(m => m.UserRecordEdit, (Guid?)Guid.Empty)
                .SetProperty(m => m.DateRecordEdit, now), ct);
    }

    private static async Task RecordFailureAsync(EventDbContext db, Guid id, string reason, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        await db.OutboxMessages
            .Where(m => m.Id == id && m.Status == OutboxStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.AttemptCount, m => m.AttemptCount + 1)
                .SetProperty(m => m.NextTryAt, now.Add(RetryDelay))
                .SetProperty(m => m.LastError, reason)
                .SetProperty(m => m.UserRecordEdit, (Guid?)Guid.Empty)
                .SetProperty(m => m.DateRecordEdit, now), ct);
    }

    private static string Sanitize(AmazonServiceException ex)
    {
        var message = string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message;
        return message.Length <= 500 ? message : message[..500];
    }
}