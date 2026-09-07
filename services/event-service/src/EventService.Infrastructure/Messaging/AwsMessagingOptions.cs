namespace EventService.Infrastructure.Messaging;

public sealed class AwsMessagingOptions
{
    public const string SectionName = "Sns";

    public string? TopicName { get; set; } = "event-created";

    public string? QueueName { get; set; } = "notification-service-event-created";
}
