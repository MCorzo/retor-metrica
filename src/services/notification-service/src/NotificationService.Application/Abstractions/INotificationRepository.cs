using NotificationService.Domain.Entities;

namespace NotificationService.Application.Abstractions;

public interface INotificationRepository
{
    Task AddAsync(NotificationRecord record, CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct);

    Task<NotificationRecord?> FindByCorrelationIdAsync(Guid correlationId, CancellationToken ct);

    Task<IReadOnlyList<NotificationRecord>> GetPendingAsync(int take, CancellationToken ct);

    Task<IReadOnlyList<NotificationRecord>> GetUnroutedFailedAsync(int take, CancellationToken ct);

    Task<IReadOnlyList<NotificationRecord>> ListAsync(CancellationToken ct);
}
