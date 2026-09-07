using Microsoft.AspNetCore.Authorization;

namespace NotificationService.Api.Auth;

public static class ApiPolicies
{
    public const string AdministratorOrOrganizer = "administrator-or-organizer";

    public static void AddNotificationServiceAuthorization(AuthorizationOptions options)
    {
        options.AddPolicy(AdministratorOrOrganizer, policy => policy
            .RequireAuthenticatedUser()
            .RequireAssertion(ctx =>
            {
                var roles = ctx.User.RolesOf();
                return roles.Contains("admin") || roles.Contains("organizer");
            }));
    }
}
