namespace EventService.Infrastructure.Messaging;

/// <summary>
/// Shared cache of provisioned broker coordinates. Populated by
/// <see cref="BrokerProvisioner"/> at startup and read by the outbox publisher
/// and relay so no AWS call is ever needed on the request path.
/// </summary>
public sealed class AwsBrokerState
{
    public string? TopicArn { get; set; }

    public string? QueueUrl { get; set; }
}