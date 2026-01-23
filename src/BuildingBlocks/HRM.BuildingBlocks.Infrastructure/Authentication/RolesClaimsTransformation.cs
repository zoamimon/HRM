using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace HRM.BuildingBlocks.Infrastructure.Authentication;

/// <summary>
/// Transforms and normalizes role claims from various formats into standard ClaimTypes.Role claims.
/// This ensures consistent role handling across the application and enables native ASP.NET Core
/// authorization features like [Authorize(Roles = "...")] and User.IsInRole().
/// </summary>
public sealed class RolesClaimsTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return Task.FromResult(principal);
        }

        var identity = principal.Identity as ClaimsIdentity;
        if (identity == null)
        {
            return Task.FromResult(principal);
        }

        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Format 1: Comma-separated "Roles" claim (our JWT format)
        var rolesFromCommaDelimited = principal
            .FindAll("Roles")
            .SelectMany(c => c.Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Select(r => r.Trim());
        roles.UnionWith(rolesFromCommaDelimited);

        // Format 2: Standard ClaimTypes.Role (already normalized)
        var rolesFromStandard = principal
            .FindAll(ClaimTypes.Role)
            .Select(c => c.Value);
        roles.UnionWith(rolesFromStandard);

        // Format 3: Custom "role" claims (lowercase, common in OAuth/OIDC)
        var rolesFromCustom = principal
            .FindAll("role")
            .Select(c => c.Value);
        roles.UnionWith(rolesFromCustom);

        // Remove all existing role claims
        var existingRoleClaims = identity
            .FindAll(c => c.Type == ClaimTypes.Role || c.Type == "Roles" || c.Type == "role")
            .ToList();

        foreach (var claim in existingRoleClaims)
        {
            identity.RemoveClaim(claim);
        }

        // Add normalized role claims using standard ClaimTypes.Role
        foreach (var role in roles.Where(r => !string.IsNullOrWhiteSpace(r)))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        return Task.FromResult(principal);
    }
}
