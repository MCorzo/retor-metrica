using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Entities;
using NotificationService.Infrastructure.Messaging;

namespace NotificationService.Infrastructure.Mail;

public sealed class EmailScanner(
    IServiceScopeFactory scopeFactory,
    EmailOptions emailOptions,
    ILogger<EmailScanner> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan TransientRetryIn = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DlqRetryIn = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Email scan cycle failed: {Reason}", EmailFailureClassifier.Sanitize(ex));
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ScanOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var relay = scope.ServiceProvider.GetRequiredService<DeadLetterRelay>();

        var pending = await repository.GetPendingAsync(take: 50, ct);
        foreach (var record in pending)
        {
            using var trace = logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = record.CorrelationId,
                ["RecordId"] = record.Id,
            });

            try
            {
                await emailSender.SendAsync(record, ct);
                logger.LogInformation("Notification email sent for {RecordId} (event {EventId})", record.Id, record.EventId);
            }
            catch (Exception ex)
            {
                var reason = EmailFailureClassifier.Sanitize(ex);
                if (IsTerminal(record, ex))
                {
                    record.MarkTerminal(reason);
                    await RouteToDlqAsync(record, relay, ct);
                    logger.LogWarning(
                        "Notification {RecordId} permanently failed or exceeded the transient retry cap ({Cap}); DLQ routing attempted",
                        record.Id, emailOptions.MaxTransientAttempts);
                }
                else
                {
                    record.RecordTransientFailure(reason, TransientRetryIn);
                    logger.LogWarning(
                        "Notification {RecordId} transient send failure, scheduled retry: {Reason}", record.Id, reason);
                }
            }

            await repository.SaveChangesAsync(ct);
        }

        await RoutePendingFailedAsync(repository, relay, ct);
    }

    private bool IsTerminal(NotificationRecord record, Exception ex) =>
        EmailFailureClassifier.IsPermanent(ex) || record.AttemptCount + 1 >= emailOptions.MaxTransientAttempts;

    private async Task RouteToDlqAsync(NotificationRecord record, DeadLetterRelay relay, CancellationToken ct)
    {
        try
        {
            var messageId = await relay.TrySendAsync(record, ct);
            if (!string.IsNullOrEmpty(messageId))
            {
                record.MarkDlqRouted(messageId);
            }
            else
            {
                record.MarkDlqRoutePending(DlqRetryIn);
            }
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException && ct.IsCancellationRequested)
            {
                throw;
            }

            record.MarkDlqRoutePending(DlqRetryIn);
            logger.LogWarning("DLQ route for record {RecordId} deferred: {Reason}", record.Id, EmailFailureClassifier.Sanitize(ex));
        }
    }

    private async Task RoutePendingFailedAsync(INotificationRepository repository, DeadLetterRelay relay, CancellationToken ct)
    {
        var unrouted = await repository.GetUnroutedFailedAsync(take: 50, ct);
        foreach (var record in unrouted)
        {
            using var trace = logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = record.CorrelationId,
                ["RecordId"] = record.Id,
            });

            await RouteToDlqAsync(record, relay, ct);
            await repository.SaveChangesAsync(ct);
        }
    }
}
