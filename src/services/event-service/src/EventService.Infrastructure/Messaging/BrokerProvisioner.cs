using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using EventService.Infrastructure.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventService.Infrastructure.Messaging;

public sealed class BrokerProvisioner(
    IAmazonSimpleNotificationService sns,
    AwsMessagingOptions options,
    AwsBrokerState broker,
    ILogger<BrokerProvisioner> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var topicName = options.TopicName ?? "event-created";

        try
        {
            var response = await sns.CreateTopicAsync(topicName, ct);
            broker.TopicArn = response.TopicArn;
            logger.LogInformation("SNS topic {Topic} ensured (ARN {Arn})", topicName, broker.TopicArn);
        }
        catch (AmazonServiceException ex) when (ex.ErrorCode == "TopicAlreadyExists")
        {
            broker.TopicArn = await ResolveExistingTopicArnAsync(topicName, ct);
            logger.LogInformation("SNS topic {Topic} already exists (ARN {Arn})", topicName, broker.TopicArn);
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private async Task<string> ResolveExistingTopicArnAsync(string topicName, CancellationToken ct)
    {
        try
        {
            var attrs = await sns.GetTopicAttributesAsync(topicName, ct);
            if (attrs.Attributes?.TryGetValue("TopicArn", out var arn) == true
                && !string.IsNullOrWhiteSpace(arn))
            {
                return arn;
            }
        }
        catch (AmazonServiceException)
        {
            // Fall through to the ListTopics scan.
        }

        var suffix = $":{topicName}";
        var topics = await sns.ListTopicsAsync(ct);
        var match = topics.Topics?.FirstOrDefault(t => t.TopicArn?.EndsWith(suffix, StringComparison.Ordinal) == true);
        return match?.TopicArn
            ?? throw new InvalidOperationException($"SNS topic '{topicName}' exists but its ARN could not be resolved.");
    }
}
