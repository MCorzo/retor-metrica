using EventService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventService.Infrastructure.Persistence;

public sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> b)
    {
        b.ToTable("events");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.Name).HasMaxLength(200).IsRequired();
        b.Property(e => e.Date).IsRequired();
        b.Property(e => e.Venue).HasMaxLength(300).IsRequired();
        b.Property(e => e.Status).HasMaxLength(20).IsRequired();
        b.Property(e => e.OwnerId).IsRequired();

        b.HasIndex(e => e.OwnerId).HasDatabaseName("IX_events_owner_id");
        b.HasIndex(e => e.Date).HasDatabaseName("IX_events_date");

        b.HasMany(e => e.Zones)
            .WithOne()
            .HasForeignKey(z => z.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Property(e => e.UserRecordCreation).IsRequired();
        b.Property(e => e.DateRecordCreation).IsRequired();
    }
}

public sealed class ZoneConfiguration : IEntityTypeConfiguration<Zone>
{
    public void Configure(EntityTypeBuilder<Zone> b)
    {
        b.ToTable("zones", t => t.HasCheckConstraint("CK_zones_price_positive", "\"price\" > 0"));
        b.HasKey(z => z.Id);
        b.Property(z => z.Id).ValueGeneratedNever();
        b.Property(z => z.EventId).IsRequired();
        b.Property(z => z.Name).HasMaxLength(100).IsRequired();
        b.Property(z => z.Price).HasPrecision(10, 2).IsRequired();
        b.Property(z => z.Capacity).IsRequired();

        b.HasIndex(z => z.EventId).HasDatabaseName("IX_zones_event_id");

        b.Property(z => z.UserRecordCreation).IsRequired();
        b.Property(z => z.DateRecordCreation).IsRequired();
    }
}
