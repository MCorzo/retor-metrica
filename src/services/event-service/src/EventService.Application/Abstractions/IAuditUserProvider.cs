namespace EventService.Application.Abstractions;

public interface IAuditUserProvider
{
    Guid? CurrentUserId { get; }

    bool IsInRole(string role);

    Guid? SubjectId { get; }
}
