using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Auditing;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence;

public sealed class AuditSaveChangesInterceptor(IAuditUserProvider auditUser) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        ApplyAudit(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyAudit(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private void ApplyAudit(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var user = auditUser.CurrentUserId ?? Guid.Empty;

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.DateRecordCreation = now;
                    entry.Entity.UserRecordCreation = user;
                    entry.Entity.DateRecordEdit = now;
                    entry.Entity.UserRecordEdit = user;
                    break;
                case EntityState.Modified:
                    entry.Entity.DateRecordEdit = now;
                    entry.Entity.UserRecordEdit = user;
                    break;
            }
        }
    }
}

public sealed class NotificationRecordConfiguration : IEntityTypeConfiguration<NotificationRecord>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<NotificationRecord> b)
    {
        b.ToTable("notification_records");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).ValueGeneratedNever();
        b.Property(r => r.EventId).IsRequired();
        b.Property(r => r.EventName).HasMaxLength(200).IsRequired();
        b.Property(r => r.EventDate).IsRequired();
        b.Property(r => r.EventVenue).HasMaxLength(300).IsRequired();
        b.Property(r => r.CorrelationId).IsRequired();
        b.Property(r => r.MessageTimestamp).IsRequired();
        b.Property(r => r.PayloadHash).HasMaxLength(64).IsRequired();
        b.Property(r => r.Status).HasMaxLength(20).IsRequired();
        b.Property(r => r.AttemptCount).HasDefaultValue(0).IsRequired();
        b.Property(r => r.LastError).HasMaxLength(500);
        b.Property(r => r.SmtpMessageId).HasMaxLength(200);
        b.Property(r => r.ZoneDetailsJson).HasColumnType("text");
        b.Property(r => r.OriginalMessageJson).HasColumnType("text");
        b.Property(r => r.DlqMessageId).HasMaxLength(128);
        b.Property(r => r.DlqRoutedAt);

        b.HasIndex(r => r.CorrelationId)
            .IsUnique()
            .HasDatabaseName("UX_notification_records_correlation_id");
        // Scanner poll index
        b.HasIndex(r => new { r.Status, r.NextTryAt })
            .HasDatabaseName("IX_notification_records_status_next_try_at");

        b.Property(r => r.UserRecordCreation).IsRequired();
        b.Property(r => r.DateRecordCreation).IsRequired();
    }
}
