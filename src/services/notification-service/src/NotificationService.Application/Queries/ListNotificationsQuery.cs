using MediatR;
using NotificationService.Application.Abstractions;
using NotificationService.Application.Dtos;
using NotificationService.Application.Mapping;

namespace NotificationService.Application.Queries;

public sealed record ListNotificationsQuery : IRequest<NotificationListResponse>;

public sealed class ListNotificationsQueryHandler(
    INotificationRepository repository) : IRequestHandler<ListNotificationsQuery, NotificationListResponse>
{
    public async Task<NotificationListResponse> Handle(ListNotificationsQuery request, CancellationToken ct)
    {
        var records = await repository.ListAsync(ct);
        return new NotificationListResponse(records.Select(NotificationViews.ToRecord).ToList());
    }
}
