using Serilog.Core;
using Serilog.Events;

namespace EventService.Api.Logging;

/// <summary>
/// Adds the current request correlation/trace id to every structured log
/// entry so a single business flow can be reconstructed across services
/// (constitution Observability section).
/// </summary>
public sealed class CorrelationIdEnricher(IHttpContextAccessor httpContextAccessor) : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var traceId = httpContextAccessor.HttpContext?.TraceIdentifier;
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
            "CorrelationId", string.IsNullOrEmpty(traceId) ? Guid.NewGuid() : traceId));
    }
}
