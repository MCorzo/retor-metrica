using System.Net.Sockets;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Mail;

/// <summary>
/// Sends a single simple-text email via MailKit through a Polly retry pipeline.
/// Transient failures (4xx/protocol/timeout) are retried; permanent 5xx
/// rejections bubble up so the scanner can mark the record Failed.
/// </summary>
public sealed class SmtpEmailSender(
    SmtpOptions options,
    EventEmailBuilder emailBuilder,
    IAuditEmailTarget emailTarget) : IEmailSender
{
    public async Task SendAsync(NotificationRecord record, CancellationToken ct)
    {
        var message = emailBuilder.Build(record);
        message.To.Clear();
        message.To.Add(new MimeKit.MailboxAddress("Admin", emailTarget.AdminAddress));

        await ResiliencePolicies.SmtpSendPipeline().ExecuteAsync(async token =>
        {
            using var client = new SmtpClient();

            await client.ConnectAsync(options.Host, options.Port,
                options.UseStartTls ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.Auto, token)
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(options.Username))
            {
                await client.AuthenticateAsync(options.Username, options.Password ?? string.Empty, token)
                    .ConfigureAwait(false);
            }

            await client.SendAsync(message, token).ConfigureAwait(false);
            await client.DisconnectAsync(true, token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(message.MessageId))
        {
            record.MarkSent(message.MessageId);
        }
        else
        {
            record.MarkSent(DateTimeOffset.UtcNow.Ticks.ToString());
        }
    }
}

/// <summary>Classifies SMTP failures as transient (retry) vs permanent (mark Failed).</summary>
public static class EmailFailureClassifier
{
    public static bool IsPermanent(Exception ex)
    {
        switch (ex)
        {
            case SmtpCommandException smtp when smtp.StatusCode is >= SmtpStatusCode.MailboxUnavailable
                or SmtpStatusCode.UserNotLocalTryAlternatePath:
                return true;
            case SmtpCommandException smtp:
                return (int)smtp.StatusCode >= 500;
            case SocketException or ServiceNotConnectedException or ProtocolException or IOException or TimeoutException:
                return false;
            default:
                return false;
        }
    }

    public static string Sanitize(Exception ex)
    {
        var reason = $"{ex.GetType().Name}: {ex.Message}";
        return reason.Length <= 500 ? reason : reason[..500];
    }
}
