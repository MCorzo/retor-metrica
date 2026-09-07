namespace NotificationService.Application.Abstractions;

public interface IAuditUserProvider
{
    Guid? CurrentUserId { get; }
}
