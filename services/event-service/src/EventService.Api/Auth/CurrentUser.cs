using EventService.Application.Abstractions;

namespace EventService.Api.Auth;

/// <summary>
/// Resolves the authenticated actor for audit fields and resource ownership
/// (constitution: resource ownership / IDOR prevention).
/// </summary>
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

    public string DominantRole => httpContextAccessor.HttpContext?.User.DominantRole() ?? "organizer";

    public bool IsInRole(string role) =>
        httpContextAccessor.HttpContext?.User.RolesOf().Contains(role) == true;
}
