using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SQS;

namespace EventService.Infrastructure.Messaging;

public static class AwsClients
{
    public static IAmazonSimpleNotificationService CreateSns(AwsCredentialsOptions options)
    {
        var region = Region(options);
        return TryStaticCredentials(options, out var credentials)
            ? new AmazonSimpleNotificationServiceClient(credentials, region)
            : new AmazonSimpleNotificationServiceClient(region);
    }

    public static IAmazonSQS CreateSqs(AwsCredentialsOptions options)
    {
        var region = Region(options);
        return TryStaticCredentials(options, out var credentials)
            ? new AmazonSQSClient(credentials, region)
            : new AmazonSQSClient(region);
    }

    private static RegionEndpoint Region(AwsCredentialsOptions options) =>
        RegionEndpoint.GetBySystemName(options.Region ?? "us-east-1");

    private static bool TryStaticCredentials(AwsCredentialsOptions options, out AWSCredentials credentials)
    {
        if (!string.IsNullOrWhiteSpace(options.AccessKey) && !string.IsNullOrWhiteSpace(options.SecretKey))
        {
            credentials = new BasicAWSCredentials(options.AccessKey, options.SecretKey);
            return true;
        }

        credentials = null!;
        return false;
    }
}
