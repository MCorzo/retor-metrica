namespace NotificationService.Infrastructure.Messaging;

public sealed class AwsCredentialsOptions
{
    public const string SectionName = "Aws";

    public string? AccessKey { get; set; }

    public string? SecretKey { get; set; }

    public string? Region { get; set; } = "us-east-1";
}
