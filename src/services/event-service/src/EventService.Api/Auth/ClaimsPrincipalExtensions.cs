using System.Security.Claims;
using System.Text.Json;

namespace EventService.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static string? SubjectId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? user.FindFirstValue("sub");

    public static HashSet<string> RolesOf(this ClaimsPrincipal user)
    {
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var claim in user.FindAll(ClaimTypes.Role).Concat(user.FindAll("role")).Concat(user.FindAll("roles")))
        {
            if (!string.IsNullOrWhiteSpace(claim.Value))
            {
                roles.Add(claim.Value);
            }
        }

        var realmAccess = user.FindFirst("realm_access")?.Value;
        if (!string.IsNullOrWhiteSpace(realmAccess))
        {
            try
            {
                using var doc = JsonDocument.Parse(realmAccess);
                if (doc.RootElement.TryGetProperty("roles", out var roleArray))
                {
                    foreach (var role in roleArray.EnumerateArray())
                    {
                        if (role.ValueKind == JsonValueKind.String)
                        {
                            roles.Add(role.GetString() ?? string.Empty);
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Unparseable realm_access — fail open on unavailable claims.
            }
        }

        return roles;
    }

    public static string DominantRole(this ClaimsPrincipal user)
    {
        var roles = user.RolesOf();
        if (roles.Contains("admin"))
        {
            return "admin";
        }

        if (roles.Contains("client"))
        {
            return "client";
        }

        return "organizer";
    }
}
