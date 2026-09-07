namespace EventService.Application.Abstractions;

/// <summary>Resolves the authenticated actor for audit fields. Implemented in the Api layer.</summary>
public interface IAuditUserProvider
{
    Guid? CurrentUserId { get; }

    bool IsInRole(string role);

    Guid? SubjectId { get; }
}
