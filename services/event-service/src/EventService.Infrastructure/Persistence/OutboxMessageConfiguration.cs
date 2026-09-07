using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventService.Infrastructure.Persistence;

/// <summary>EF Core mapping for the application-owned outbox (data-model.md).</summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("outbox_messages");
        b.HasKey(m => m.Id);
        b.Property(m => m.Id).ValueGeneratedNever();
        b.Property(m => m.MessageId).IsRequired();
        b.Property(m => m.TopicArn).HasMaxLength(300).IsRequired();
        b.Property(m => m.MessageType).HasMaxLength(100).IsRequired();
        b.Property(m => m.Payload).HasColumnType("jsonb").IsRequired();
        b.Property(m => m.CorrelationId).IsRequired();
        b.Property(m => m.Status).HasMaxLength(20).IsRequired();
        b.Property(m => m.AttemptCount).HasDefaultValue(0).IsRequired();
        b.Property(m => m.NextTryAt).IsRequired();
        b.Property(m => m.LastError).HasMaxLength(500);
        b.Property(m => m.PublishedAt);

        b.HasIndex(m => m.MessageId).IsUnique().HasDatabaseName("UX_outbox_messages_message_id");
        b.HasIndex(m => new { m.Status, m.NextTryAt }).HasDatabaseName("IX_outbox_messages_status_next_try_at");
        b.HasIndex(m => m.CorrelationId).HasDatabaseName("IX_outbox_messages_correlation_id");

        b.Property(m => m.UserRecordCreation).IsRequired();
        b.Property(m => m.DateRecordCreation).IsRequired();
    }
}