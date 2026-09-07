using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Abstractions;

namespace NotificationService.Infrastructure.Mail;

/// <summary>
/// Record-as-outbox scanner (research.md decision 3): polls due <c>Pending</c>
/// records and dispatches email via the SMTP sender. Failures are recorded on
/// the record (retry or terminal Failed) — data is never lost (FR-016).
/// </summary>
public sealed class EmailScanner(
    IServiceScopeFactory scopeFactory,
    ILogger<EmailScanner> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan TransientRetryIn = TimeSpan.FromSeconds(30);

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

        var pending = await repository.GetPendingAsync(take: 50, ct);
        foreach (var record in pending)
        {
            try
            {
                await emailSender.SendAsync(record, ct);
                logger.LogInformation("Notification email sent for {RecordId} (event {EventId})", record.Id, record.EventId);
            }
            catch (Exception ex)
            {
                var reason = EmailFailureClassifier.Sanitize(ex);
                if (EmailFailureClassifier.IsPermanent(ex))
                {
                    record.RecordFailure(reason, TimeSpan.FromHours(1));
                    logger.LogWarning("Notification {RecordId} permanently failed: {Reason}", record.Id, reason);
                }
                else
                {
                    record.RecordTransientFailure(reason, TransientRetryIn);
                    logger.LogWarning("Notification {RecordId} transient send failure, scheduled retry: {Reason}", record.Id, reason);
                }
            }

            await repository.SaveChangesAsync(ct);
        }
    }
}
