namespace EventService.Infrastructure.Messaging;

public sealed class AwsBrokerState
{
    public string? TopicArn { get; set; }

    public string? QueueUrl { get; set; }
}
