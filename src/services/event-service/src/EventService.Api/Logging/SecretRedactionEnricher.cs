using Serilog.Core;
using Serilog.Events;

namespace EventService.Api.Logging;

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
