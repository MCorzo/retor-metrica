using System.Globalization;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Messaging;

public sealed class DeadLetterRelay(
    IAmazonSQS sqs,
    AwsBrokerState broker,
    ILogger<DeadLetterRelay> logger)
{
    private const string EnvelopeVersion = "1";

    public async Task<string?> TrySendAsync(NotificationRecord record, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(broker.DlqUrl))
        {
            logger.LogWarning(
                "DLQ routing deferred for record {RecordId} (correlation {CorrelationId}): DLQ not provisioned",
                record.Id, record.CorrelationId);
            return null;
        }

        var body = record.OriginalMessageJson;
        if (string.IsNullOrWhiteSpace(body))
        {
            logger.LogWarning(
                "DLQ routing skipped for record {RecordId} (correlation {CorrelationId}): original message body missing",
                record.Id, record.CorrelationId);
            return null;
        }

        var routedAt = DateTimeOffset.UtcNow;
        var request = new SendMessageRequest
        {
            QueueUrl = broker.DlqUrl,
            MessageBody = body,
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                ["CorrelationId"] = Attr(record.CorrelationId.ToString("D", CultureInfo.InvariantCulture)),
                ["EventId"] = Attr(record.EventId.ToString("D", CultureInfo.InvariantCulture)),
                ["RecordId"] = Attr(record.Id.ToString("D", CultureInfo.InvariantCulture)),
                // FailureReason must be <= 250 chars (contract) — LastError is already sanitized (<=500).
                ["FailureReason"] = Attr(ShortReason(record.LastError)),
                ["RoutedAt"] = Attr(routedAt.ToString("o", CultureInfo.InvariantCulture)),
                ["EnvelopeVersion"] = Attr(EnvelopeVersion, isNumber: true),
            },
        };

        var response = await _pipeline.ExecuteAsync(
            async token => await sqs.SendMessageAsync(request, token), ct).ConfigureAwait(false);

        logger.LogInformation(
            "DLQ envelope sent for record {RecordId} (correlation {CorrelationId}) as SQS message {MessageId}",
            record.Id, record.CorrelationId, response.MessageId);
        return response.MessageId;
    }

    private static MessageAttributeValue Attr(string value, bool isNumber = false) => new()
    {
        DataType = isNumber ? "Number" : "String",
        StringValue = value,
    };

    private static string ShortReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return "unknown";
        }

        return reason.Length <= 250 ? reason : reason[..250];
    }

    private static readonly ResiliencePipeline _pipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            ShouldHandle = new PredicateBuilder()
                .Handle<AmazonServiceException>(IsTransientSqs)
                .Handle<TimeoutException>()
                .Handle<System.Net.Http.HttpRequestException>()
                .Handle<System.Net.Sockets.SocketException>(),
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(250),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
        })
        .Build();

    private static bool IsTransientSqs(AmazonServiceException ex) =>
        ex.StatusCode is System.Net.HttpStatusCode.InternalServerError
            or System.Net.HttpStatusCode.BadGateway
            or System.Net.HttpStatusCode.ServiceUnavailable
        || ex.ErrorCode?.Contains("Throttl", StringComparison.OrdinalIgnoreCase) == true
        || ex.ErrorCode == "InternalError"
        || ex.ErrorCode == "PriorRequestNotComplete";
}
