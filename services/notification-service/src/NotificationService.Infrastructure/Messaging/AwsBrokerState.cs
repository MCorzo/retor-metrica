namespace NotificationService.Infrastructure.Messaging;

/// <summary>
/// Shared cache of provisioned broker coordinates. Populated by
/// <see cref="BrokerProvisioner"/> at startup and read by the SQS consumer
/// worker so no AWS lookup is needed on the poll path.
/// </summary>
public sealed class AwsBrokerState
{
    public string? TopicArn { get; set; }

    public string? QueueUrl { get; set; }
}