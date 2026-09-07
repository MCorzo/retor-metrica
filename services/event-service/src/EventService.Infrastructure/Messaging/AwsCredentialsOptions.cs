namespace EventService.Infrastructure.Messaging;

/// <summary>
/// Binds the <c>Aws</c> configuration section — the shared AWS static
/// credentials and region used by SNS, SQS, and CloudWatch. Values stay empty
/// in tracked files and are injected via environment variables.
/// </summary>
public sealed class AwsCredentialsOptions
{
    public const string SectionName = "Aws";

    public string? AccessKey { get; set; }

    public string? SecretKey { get; set; }

    public string? Region { get; set; } = "us-east-1";
}