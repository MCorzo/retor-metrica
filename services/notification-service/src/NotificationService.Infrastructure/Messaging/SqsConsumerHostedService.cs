using System.Text.Json;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using EventPlatform.Contracts.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NotificationService.Infrastructure.Messaging;

public sealed class SqsConsumerHostedService(
    IServiceScopeFactory scopeFactory,
    IAmazonSQS sqs,
    AwsBrokerState broker,
    ILogger<SqsConsumerHostedService> logger) : BackgroundService
{
    private const int LongPollSeconds = 20;
    private const int MaxMessages = 10;
    private const int VisibilityTimeout = 30;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var queueUrl = broker.QueueUrl;
                if (string.IsNullOrWhiteSpace(queueUrl))
                {
                    // BrokerProvisioner runs first (registration order); wait defensively.
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    continue;
                }

                var receive = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = queueUrl,
                    MaxNumberOfMessages = MaxMessages,
                    WaitTimeSeconds = LongPollSeconds,
                    VisibilityTimeout = VisibilityTimeout,
                }, stoppingToken);

                foreach (var message in receive.Messages ?? [])
                {
                    await ProcessMessageAsync(queueUrl, message, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (AmazonServiceException ex)
            {
                logger.LogWarning("SQS receive failed: {Reason}; retrying", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ProcessMessageAsync(string queueUrl, Message message, CancellationToken ct)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<EventCreated>(message.Body, JsonOptions);
            if (parsed is null || parsed.CorrelationId == Guid.Empty)
            {
                logger.LogWarning("Poison EventCreated ignored and deleted (message {MessageId})", message.MessageId);
                await DeleteMessageAsync(queueUrl, message, ct);
                return;
            }

            using var scope = scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<EventCreatedProcessor>();
            await processor.HandleAsync(parsed, message.Body, ct);

            await DeleteMessageAsync(queueUrl, message, ct);
        }
        catch (JsonException)
        {
            logger.LogWarning("Unparseable SQS message {MessageId} deleted as poison", message.MessageId);
            await DeleteMessageAsync(queueUrl, message, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
    }

    private async Task DeleteMessageAsync(string queueUrl, Message message, CancellationToken ct)
    {
        try
        {
            await sqs.DeleteMessageAsync(queueUrl, message.ReceiptHandle, ct);
        }
        catch (AmazonServiceException ex)
        {
            // Redelivery will retry; nothing to do beyond logging.
            logger.LogWarning("SQS delete failed for message {MessageId}: {Reason}", message.MessageId, ex.Message);
        }
    }
}
