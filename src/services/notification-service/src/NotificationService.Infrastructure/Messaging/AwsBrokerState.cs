namespace NotificationService.Infrastructure.Messaging;

public sealed class AwsBrokerState
{
    public string? TopicArn { get; set; }

    public string? QueueUrl { get; set; }

    public string? DlqUrl { get; set; }

    public string? DlqArn { get; set; }
}
