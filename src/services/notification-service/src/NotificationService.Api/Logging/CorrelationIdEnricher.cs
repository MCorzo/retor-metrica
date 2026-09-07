using Serilog.Core;
using Serilog.Events;

namespace NotificationService.Api.Logging;

public sealed class CorrelationIdEnricher(IHttpContextAccessor httpContextAccessor) : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var traceId = httpContextAccessor.HttpContext?.TraceIdentifier;
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
            "CorrelationId", string.IsNullOrEmpty(traceId) ? Guid.NewGuid() : traceId));
    }
}
