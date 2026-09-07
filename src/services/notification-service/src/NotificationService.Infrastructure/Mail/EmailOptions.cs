namespace NotificationService.Infrastructure.Mail;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public int MaxTransientAttempts { get; set; } = 5;
}
