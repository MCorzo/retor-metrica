using EventService.Application.Abstractions;
using EventService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventService.Infrastructure.Persistence;

public sealed class EventRepository(EventDbContext db) : IEventRepository
{
    public Task AddAsync(Event entity, CancellationToken ct)
    {
        db.Add(entity);
        return Task.CompletedTask;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);

    public async Task<Event?> GetByIdWithZonesAsync(Guid id, CancellationToken ct) =>
        await db.Events.Include(e => e.Zones).FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<Event>> QueryAsync(
        EventScope scope,
        Guid? ownerId,
        string? status,
        DateTimeOffset? after,
        int limit,
        CancellationToken ct)
    {
        IQueryable<Event> query = db.Events.Include(e => e.Zones).AsNoTracking();

        query = scope switch
        {
            EventScope.Admin => query,
            EventScope.Organizer => query.Where(e => e.OwnerId == ownerId),
            EventScope.Client => query.Where(e => e.Status == "published"),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null),
        };

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(e => e.Status == status);
        }

        if (after.HasValue)
        {
            query = query.Where(e => e.Date >= after.Value);
        }

        return await query.OrderBy(e => e.Date).Take(limit).ToListAsync(ct);
    }
}
