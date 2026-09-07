using EventService.Application.Abstractions;
using EventService.Application.Dtos;
using EventService.Application.Mapping;
using MediatR;

namespace EventService.Application.Queries;

public sealed record GetEventQuery(Guid Id, EventScope Scope, Guid? OwnerId) : IRequest<GetEventResult?>;

public sealed record GetEventResult(EventDto Event);

public sealed class GetEventQueryHandler(
    IEventRepository repository) : IRequestHandler<GetEventQuery, GetEventResult?>
{
    public async Task<GetEventResult?> Handle(GetEventQuery request, CancellationToken ct)
    {
        var @event = await repository.GetByIdWithZonesAsync(request.Id, ct);
        if (@event is null)
        {
            return null;
        }

        if (!CanAccess(@event, request.Scope, request.OwnerId))
        {
            // Do not leak existence across ownership boundaries (IDOR-safe).
            return null;
        }

        return new GetEventResult(EventViews.ToEvent(@event));
    }

    private static bool CanAccess(Domain.Entities.Event @event, EventScope scope, Guid? ownerId) => scope switch
    {
        EventScope.Admin => true,
        EventScope.Organizer => @event.OwnerId == ownerId,
        EventScope.Client => @event.Status == "published",
        _ => false,
    };
}
