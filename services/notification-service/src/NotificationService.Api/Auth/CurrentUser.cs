using NotificationService.Application.Abstractions;

namespace NotificationService.Api.Auth;

/// <summary>Resolves the authenticated actor for audit fields.</summary>
public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : IAuditUserProvider
{
    public Guid? SubjectId
    {
        get
        {
            var sub = httpContextAccessor.HttpContext?.User.SubjectId();
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public Guid? CurrentUserId => SubjectId;

    public bool IsInRole(string role) =>
        httpContextAccessor.HttpContext?.User.RolesOf().Contains(role) == true;
}
