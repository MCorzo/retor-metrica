using EventService.Application.Abstractions;
using EventService.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EventService.Infrastructure.Persistence;

/// <summary>
/// Automatically maintains the mandatory audit fields (constitution v1.8.0)
/// on every insert/update. Creation fields are set once; edit fields mirror
/// creation on insert and are overwritten on every update.
/// </summary>
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
