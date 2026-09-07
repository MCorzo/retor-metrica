namespace NotificationService.Application.Abstractions;

public interface IEmailSender
{
    Task SendAsync(Domain.Entities.NotificationRecord record, CancellationToken ct);
}
