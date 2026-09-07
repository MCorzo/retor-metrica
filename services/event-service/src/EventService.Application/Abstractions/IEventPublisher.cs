using EventPlatform.Contracts.Messages;

namespace EventService.Application.Abstractions;

public interface IEventPublisher
{
    Task PublishAsync(EventCreated message, CancellationToken ct);
}
