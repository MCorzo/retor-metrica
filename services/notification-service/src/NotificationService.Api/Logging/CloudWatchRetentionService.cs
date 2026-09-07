using Amazon;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.Runtime;

namespace NotificationService.Api.Logging;

public sealed class CloudWatchRetentionService(
    IConfiguration configuration,
    ILogger<CloudWatchRetentionService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        if (!configuration.GetSection("CloudWatch:Enabled").Get<bool>())
        {
            return;
        }

        var logGroup = configuration["CloudWatch:LogGroup"] ?? "event-platform/NotificationService";
        var region = configuration["Aws:Region"] ?? "us-east-1";

        using var client = CreateClient(region);
        try
        {
            await client.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = logGroup }, ct);
        }
        catch (ResourceAlreadyExistsException)
        {
            // Group already exists (or the sink just created it).
        }

        await client.PutRetentionPolicyAsync(
            new PutRetentionPolicyRequest { LogGroupName = logGroup, RetentionInDays = 30 }, ct);
        logger.LogInformation("CloudWatch log group {Group} retention set to 30 days.", logGroup);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private AmazonCloudWatchLogsClient CreateClient(string region)
    {
        var accessKey = configuration["Aws:AccessKey"];
        var secretKey = configuration["Aws:SecretKey"];
        var endpoint = RegionEndpoint.GetBySystemName(region);

        return !string.IsNullOrWhiteSpace(accessKey) && !string.IsNullOrWhiteSpace(secretKey)
            ? new AmazonCloudWatchLogsClient(new BasicAWSCredentials(accessKey, secretKey), endpoint)
            : new AmazonCloudWatchLogsClient(endpoint);
    }
}
