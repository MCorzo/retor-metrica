using NotificationService.Domain.Entities;

namespace NotificationService.Application.Abstractions;

/// <summary>Persistence port for notification records. Implemented by NotificationService.Infrastructure.</summary>
public interface INotificationRepository
{
    Task AddAsync(NotificationRecord record, CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct);

    Task<NotificationRecord?> FindByCorrelationIdAsync(Guid correlationId, CancellationToken ct);

    Task<IReadOnlyList<NotificationRecord>> GetPendingAsync(int take, CancellationToken ct);

    Task<IReadOnlyList<NotificationRecord>> ListAsync(CancellationToken ct);
}
