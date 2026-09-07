namespace NotificationService.Application.Abstractions;

/// <summary>Sends the notification email for a record. Throws on failure.</summary>
public interface IEmailSender
{
    Task SendAsync(Domain.Entities.NotificationRecord record, CancellationToken ct);
}
