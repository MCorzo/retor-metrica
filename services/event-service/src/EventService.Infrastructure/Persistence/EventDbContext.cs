using EventService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Event = EventService.Domain.Entities.Event;

namespace EventService.Infrastructure.Persistence;

/// <summary>EventService store — schema <c>events</c>. Data access via EF Core only (constitution).</summary>
public sealed class EventDbContext(DbContextOptions<EventDbContext> options) : DbContext(options)
{
    public DbSet<Event> Events => Set<Event>();

    public DbSet<Zone> Zones => Set<Zone>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("events");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EventDbContext).Assembly);
    }
}
