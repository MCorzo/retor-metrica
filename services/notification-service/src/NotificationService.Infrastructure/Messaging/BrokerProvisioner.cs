using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.SQS.Util;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NotificationService.Infrastructure.Messaging;

public sealed class BrokerProvisioner(
    IAmazonSimpleNotificationService sns,
    IAmazonSQS sqs,
    AwsMessagingOptions options,
    AwsBrokerState broker,
    ILogger<BrokerProvisioner> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var topicName = options.TopicName ?? "event-created";
        var queueName = options.QueueName ?? "notification-service-event-created";

        broker.TopicArn = await EnsureTopicAsync(topicName, ct);
        broker.QueueUrl = await EnsureQueueAsync(broker.TopicArn, queueName, ct);
        await EnsureSubscriptionAsync(broker.TopicArn, broker.QueueUrl, ct);

        var dlqName = options.DlqName ?? "notification-service-event-created-dlq";
        (broker.DlqUrl, broker.DlqArn) = await EnsureDlqAsync(dlqName, ct);

        logger.LogInformation(
            "AWS messaging provisioned: SNS {Topic}, SQS {Queue} (raw subscription), DLQ {DlqUrl} (retention 14 days)",
            broker.TopicArn, broker.QueueUrl, broker.DlqUrl);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private async Task<string> EnsureTopicAsync(string topicName, CancellationToken ct)
    {
        try
        {
            var created = await sns.CreateTopicAsync(topicName, ct);
            return created.TopicArn;
        }
        catch (AmazonServiceException ex) when (ex.ErrorCode == "TopicAlreadyExists")
        {
            var attrs = await sns.GetTopicAttributesAsync(topicName, ct);
            return attrs.Attributes?.GetValueOrDefault("TopicArn")
                ?? throw new InvalidOperationException($"SNS topic '{topicName}' exists but its ARN could not be resolved.");
        }
    }

    private async Task<string> EnsureQueueAsync(string topicArn, string queueName, CancellationToken ct)
    {
        string queueUrl;
        try
        {
            var url = await sqs.GetQueueUrlAsync(queueName, ct);
            queueUrl = url.QueueUrl;
        }
        catch (QueueDoesNotExistException)
        {
            var created = await sqs.CreateQueueAsync(new CreateQueueRequest { QueueName = queueName }, ct);
            queueUrl = created.QueueUrl;
        }

        var queueArn = await QueueArnAsync(queueUrl, ct);
        await sqs.SetQueueAttributesAsync(queueUrl, new Dictionary<string, string>
        {
            ["Policy"] = QueuePolicyJson(topicArn, queueArn),
        }, ct);

        return queueUrl;
    }

    private async Task<string> QueueArnAsync(string queueUrl, CancellationToken ct)
    {
        var attrs = await sqs.GetQueueAttributesAsync(
            queueUrl, new List<string> { QueueAttributeName.QueueArn }, ct);
        return attrs.Attributes?.GetValueOrDefault(QueueAttributeName.QueueArn)
            ?? throw new InvalidOperationException($"SQS queue '{queueUrl}' reported no ARN.");
    }

    private async Task EnsureSubscriptionAsync(string topicArn, string queueUrl, CancellationToken ct)
    {
        var queueArn = await QueueArnAsync(queueUrl, ct);

        var existing = await sns.ListSubscriptionsByTopicAsync(topicArn, ct);
        if (existing.Subscriptions?.Any(s =>
                s.Protocol == "sqs" &&
                string.Equals(s.Endpoint, queueArn, StringComparison.Ordinal)) == true)
        {
            logger.LogDebug("SNS→SQS subscription already exists for {Queue}", queueArn);
            return;
        }

        await sns.SubscribeAsync(new SubscribeRequest
        {
            TopicArn = topicArn,
            Protocol = "sqs",
            Endpoint = queueArn,
            Attributes = new Dictionary<string, string> { ["RawMessageDelivery"] = "true" },
        }, ct);
    }

    private async Task<(string Url, string Arn)> EnsureDlqAsync(string dlqName, CancellationToken ct)
    {
        string dlqUrl;
        try
        {
            var url = await sqs.GetQueueUrlAsync(dlqName, ct);
            dlqUrl = url.QueueUrl;
        }
        catch (QueueDoesNotExistException)
        {
            var created = await sqs.CreateQueueAsync(new CreateQueueRequest { QueueName = dlqName }, ct);
            dlqUrl = created.QueueUrl;
        }

        var dlqArn = await QueueArnAsync(dlqUrl, ct);
        await sqs.SetQueueAttributesAsync(dlqUrl, new Dictionary<string, string>
        {
            [QueueAttributeName.MessageRetentionPeriod] = "1209600",
        }, ct);

        logger.LogDebug("DLQ ready: {DlqUrl} (retention 1209600s, no redrive policy)", dlqUrl);
        return (dlqUrl, dlqArn);
    }

    private static string QueuePolicyJson(string topicArn, string queueArn)
    {
        var policy = new
        {
            Version = "2008-10-17",
            Id = "SnsToSqsSendMessagePolicy",
            Statement = new[]
            {
                new
                {
                    Sid = "AllowSnsToSendMessages",
                    Effect = "Allow",
                    Principal = new { Service = "sns.amazonaws.com" },
                    Action = "sqs:SendMessage",
                    Resource = queueArn,
                    Condition = new { ArnEquals = new { aws_SourceArn = topicArn } },
                },
            },
        };

        var json = System.Text.Json.JsonSerializer.Serialize(policy);
        return json.Replace("aws_SourceArn", "aws:SourceArn", StringComparison.Ordinal);
    }
}
