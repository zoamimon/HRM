using System.Security.Claims;
using HRM.BuildingBlocks.Application.Abstractions.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace HRM.BuildingBlocks.Infrastructure.Authorization;

/// <summary>
/// Authorization handler for permission-based access control
///
/// Design:
/// - Handles PermissionRequirement checks
/// - Uses IPermissionService to verify user permissions
/// - Supports super admin bypass
/// - Logs authorization decisions for auditing
///
/// Flow:
/// 1. Extract user ID from claims
/// 2. Check if super admin (bypass all checks)
/// 3. Check specific permission via IPermissionService
/// 4. Succeed or fail the requirement
///
/// Usage:
/// Automatically invoked by ASP.NET Core Authorization middleware
/// when [HasPermission] or [Authorize(Policy = "...")] is used
/// </summary>
public sealed class PermissionAuthorizationHandler
    : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionService _permissionService;
    private readonly ILogger<PermissionAuthorizationHandler> _logger;

    public PermissionAuthorizationHandler(
        IPermissionService permissionService,
        ILogger<PermissionAuthorizationHandler> logger)
    {
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // 1. Get user ID from claims
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning(
                "Authorization failed: No user ID in claims for permission {Permission}",
                requirement.Permission);
            return; // Fail silently, let other handlers try
        }

        // 2. Check if super admin (bypass all permission checks)
        if (await _permissionService.IsSuperAdminAsync(userId))
        {
            _logger.LogDebug(
                "Super admin bypass for user {UserId}, permission {Permission}",
                userId,
                requirement.Permission);

            context.Succeed(requirement);
            return;
        }

        // 3. Check specific permission
        var hasPermission = await _permissionService.HasPermissionAsync(
            userId,
            requirement.Module,
            requirement.Entity,
            requirement.Action);

        if (hasPermission)
        {
            _logger.LogDebug(
                "Authorization succeeded for user {UserId}, permission {Permission}",
                userId,
                requirement.Permission);

            context.Succeed(requirement);
        }
        else
        {
            _logger.LogWarning(
                "Authorization failed for user {UserId}, permission {Permission}",
                userId,
                requirement.Permission);

            // Don't call context.Fail() - let the framework handle it
            // This allows other handlers to potentially succeed
        }
    }
}
