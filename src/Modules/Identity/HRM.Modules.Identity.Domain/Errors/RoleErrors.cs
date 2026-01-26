using HRM.BuildingBlocks.Domain.Abstractions.Results;

namespace HRM.Modules.Identity.Domain.Errors;

/// <summary>
/// Domain errors for Role entity operations
///
/// Follows pure domain principles:
/// - Transport-agnostic (no HTTP concepts)
/// - Reusable across all contexts (API, CLI, background jobs)
/// - Business-focused error messages
///
/// Error Code Convention: "Role.{ErrorName}"
///
/// HTTP Mapping (handled in API layer):
/// - NotFound → 404
/// - ConflictError → 409
/// - ValidationError → 400
/// </summary>
public static class RoleErrors
{
    /// <summary>
    /// Role with specified ID was not found
    /// Used when querying for specific role that doesn't exist
    /// </summary>
    public static NotFoundError NotFound(Guid id) =>
        new("Role.NotFound", $"Role with ID '{id}' was not found");

    /// <summary>
    /// Role with specified name was not found
    /// Used when querying for specific role by name
    /// </summary>
    public static NotFoundError NotFoundByName(string name) =>
        new("Role.NotFound", $"Role with name '{name}' was not found");

    /// <summary>
    /// Role name already exists (duplicate)
    ///
    /// Fail-Fast Strategy:
    /// This error is returned when attempting to create a role with a name that already exists.
    /// The system immediately rejects the operation instead of trying to resolve the conflict.
    ///
    /// Conflict Resolution: FAIL-FAST
    /// - No automatic name modification (e.g., appending numbers)
    /// - No merging with existing role
    /// - No silent acceptance
    /// - User must provide a different name
    /// </summary>
    public static ConflictError NameAlreadyExists(string name) =>
        new("Role.NameAlreadyExists", $"Role with name '{name}' already exists. Please choose a different name.");

    /// <summary>
    /// Duplicate permission detected in role
    ///
    /// Fail-Fast Strategy:
    /// This error is returned when attempting to add a permission that already exists in the role.
    /// The system immediately rejects the operation.
    ///
    /// Conflict Resolution: FAIL-FAST
    /// - No duplicate removal
    /// - No permission merging
    /// - No silent skipping
    /// - User must remove duplicate before proceeding
    ///
    /// Example: Adding "Personnel.Employee.View (Department)" twice to the same role
    /// </summary>
    public static ConflictError DuplicatePermission(string permissionKey, string? scope = null)
    {
        var scopeText = scope != null ? $" with scope '{scope}'" : "";
        return new("Role.DuplicatePermission",
            $"Permission '{permissionKey}'{scopeText} is already assigned to this role. Duplicate permissions are not allowed.");
    }

    /// <summary>
    /// Role cannot be deleted because it has active user assignments
    /// Business rule: Cannot delete role that is currently assigned to users
    /// </summary>
    public static ConflictError HasActiveAssignments(string roleName, int userCount) =>
        new("Role.HasActiveAssignments",
            $"Cannot delete role '{roleName}' because it is assigned to {userCount} user(s). Remove all user assignments first.");

    /// <summary>
    /// Role is empty (no permissions assigned)
    /// Business rule validation: Role must have at least one permission
    /// </summary>
    public static ValidationError EmptyRole() =>
        new("Role.EmptyRole",
            "Role must have at least one permission. Please add permissions before saving.");

    /// <summary>
    /// Role name is invalid (too short, too long, or invalid characters)
    /// </summary>
    public static ValidationError InvalidName(string reason) =>
        new("Role.InvalidName", $"Role name is invalid: {reason}");

    /// <summary>
    /// Permission does not exist in catalog
    /// Validation error when attempting to add permission that isn't defined in Permission Catalog
    /// </summary>
    public static ValidationError PermissionNotInCatalog(string module, string entity, string action) =>
        new("Role.PermissionNotInCatalog",
            $"Permission '{module}.{entity}.{action}' does not exist in the Permission Catalog. Please select a valid permission.");

    /// <summary>
    /// Scope not allowed for this action
    /// Validation error when attempting to assign a scope that isn't available for the action
    /// Example: Assigning "Department" scope to an action that only supports "Company" and "Self"
    /// </summary>
    public static ValidationError ScopeNotAllowed(string permissionKey, string scope) =>
        new("Role.ScopeNotAllowed",
            $"Scope '{scope}' is not allowed for permission '{permissionKey}'. Please select a valid scope for this action.");
}
