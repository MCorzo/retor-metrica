namespace NotificationService.Infrastructure.Messaging;

public sealed class AwsMessagingOptions
{
    public const string SectionName = "Sns";

    public string? TopicName { get; set; } = "event-created";

    public string? QueueName { get; set; } = "notification-service-event-created";

    public string? DlqName { get; set; } = "notification-service-event-created-dlq";
}
