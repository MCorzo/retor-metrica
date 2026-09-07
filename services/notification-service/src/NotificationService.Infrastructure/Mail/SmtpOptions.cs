namespace NotificationService.Infrastructure.Mail;

public sealed class SmtpOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public bool UseStartTls { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "noreply@event-platform.local";
    public string FromName { get; set; } = "Event Platform";
}
