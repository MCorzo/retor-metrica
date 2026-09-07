using EventService.Application.Abstractions;

namespace EventService.Api.Auth;

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
