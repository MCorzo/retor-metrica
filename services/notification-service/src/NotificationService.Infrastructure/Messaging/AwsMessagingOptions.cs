namespace NotificationService.Infrastructure.Messaging;

/// <summary>
/// Binds the <c>Sns</c> configuration section for direct AWS SNS/SQS access
/// (no emulator endpoint) — naming only. Credentials and region live in the
/// shared <c>Aws</c> section (<see cref="AwsCredentialsOptions"/>).
/// </summary>
public sealed class AwsMessagingOptions
{
    public const string SectionName = "Sns";

    public string? TopicName { get; set; } = "event-created";

    public string? QueueName { get; set; } = "notification-service-event-created";
}