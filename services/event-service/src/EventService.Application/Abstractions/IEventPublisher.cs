using EventPlatform.Contracts.Messages;

namespace EventService.Application.Abstractions;

/// <summary>
/// Outbound messaging port. Implemented in Infrastructure as a transactional
/// outbox writer: the row is added to the current EF Core transaction and
/// committed with the event, and the relay ships it to SNS after commit
/// (FR-006).
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync(EventCreated message, CancellationToken ct);
}