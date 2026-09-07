using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence;

public sealed class NotificationRepository(NotificationDbContext db) : INotificationRepository
{
    public Task AddAsync(NotificationRecord record, CancellationToken ct)
    {
        db.Add(record);
        return Task.CompletedTask;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);

    public async Task<NotificationRecord?> FindByCorrelationIdAsync(Guid correlationId, CancellationToken ct) =>
        await db.NotificationRecords.FirstOrDefaultAsync(r => r.CorrelationId == correlationId, ct);

    public async Task<IReadOnlyList<NotificationRecord>> GetPendingAsync(int take, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        return await db.NotificationRecords
            .Where(r => r.Status == NotificationStatus.Pending && r.NextTryAt <= now)
            .OrderBy(r => r.NextTryAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<NotificationRecord>> ListAsync(CancellationToken ct) =>
        await db.NotificationRecords
            .AsNoTracking()
            .OrderByDescending(r => r.MessageTimestamp)
            .ToListAsync(ct);
}
