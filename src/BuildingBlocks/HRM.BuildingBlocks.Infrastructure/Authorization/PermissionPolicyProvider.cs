using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace HRM.BuildingBlocks.Infrastructure.Authorization;

/// <summary>
/// Dynamic policy provider for permission-based authorization
///
/// Design:
/// - Creates policies on-demand from permission strings
/// - Avoids need to register all permissions upfront
/// - Supports HasPermissionAttribute's policy naming convention
///
/// Policy Naming:
/// - Format: "Permission:{Module}.{Entity}.{Action}"
/// - Example: "Permission:Personnel.Employee.View"
///
/// How it works:
/// 1. ASP.NET Core calls GetPolicyAsync with policy name
/// 2. If name starts with "Permission:", extract permission parts
/// 3. Create policy with PermissionRequirement
/// 4. Return policy for authorization handler
///
/// Fallback:
/// - Non-permission policies delegate to DefaultAuthorizationPolicyProvider
/// </summary>
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
