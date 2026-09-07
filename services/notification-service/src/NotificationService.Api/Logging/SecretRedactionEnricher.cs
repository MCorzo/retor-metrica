using Serilog.Core;
using Serilog.Events;

namespace NotificationService.Api.Logging;

/// <summary>
/// Guarantees AWS credentials/secret values are never serialized into log
/// events (US2): any property whose scalar value equals a configured secret is
/// replaced with <c>[REDACTED]</c> at emission time.
/// </summary>
public sealed class SecretRedactionEnricher(IEnumerable<string> secrets) : ILogEventEnricher
{
    private readonly HashSet<string> _secrets = new(
        secrets.Where(s => !string.IsNullOrWhiteSpace(s)), StringComparer.Ordinal);

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (_secrets.Count == 0)
        {
            return;
        }

        foreach (var property in logEvent.Properties.ToList())
        {
            if (property.Value is ScalarValue { Value: string value } && _secrets.Contains(value))
            {
                logEvent.RemovePropertyIfPresent(property.Key);
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(property.Key, "[REDACTED]"));
            }
        }
    }
}