using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace HRM.BuildingBlocks.Infrastructure.Authorization;

/// <summary>
/// [DEPRECATED] Dynamic policy provider for permission-based authorization
///
/// IMPORTANT: This provider is deprecated. Use RoutePermissionMiddleware instead.
/// RouteSecurityMap.xml is the Single Source of Truth for endpoint authorization.
///
/// Migration:
/// - Remove AddPermissionAuthorization() from Program.cs
/// - Add routes to RouteSecurityMap.xml
/// - RoutePermissionMiddleware handles authorization automatically
/// </summary>
[Obsolete("Use RoutePermissionMiddleware instead. RouteSecurityMap.xml is the Single Source of Truth.")]
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallbackPolicyProvider;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    /// <summary>
    /// Get policy by name - creates permission policy if applicable
    /// </summary>
    public async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        // Check if this is a permission policy
        if (policyName.StartsWith(HasPermissionAttribute.PolicyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            // Extract permission string: "Permission:Module.Entity.Action" -> "Module.Entity.Action"
            var permission = policyName[HasPermissionAttribute.PolicyPrefix.Length..];

            // Create requirement from permission string
            var requirement = PermissionRequirement.FromPermissionString(permission);

            // Build and return policy
            return new AuthorizationPolicyBuilder()
                .AddRequirements(requirement)
                .Build();
        }

        // Fallback to default provider for non-permission policies
        return await _fallbackPolicyProvider.GetPolicyAsync(policyName);
    }

    /// <summary>
    /// Get default policy (requires authenticated user)
    /// </summary>
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return _fallbackPolicyProvider.GetDefaultPolicyAsync();
    }

    /// <summary>
    /// Get fallback policy (used when no policy specified)
    /// </summary>
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return _fallbackPolicyProvider.GetFallbackPolicyAsync();
    }
}
