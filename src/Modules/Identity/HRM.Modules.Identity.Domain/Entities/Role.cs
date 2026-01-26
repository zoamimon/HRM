using HRM.BuildingBlocks.Domain.Entities;
using HRM.Modules.Identity.Domain.Events;
using HRM.Modules.Identity.Domain.ValueObjects;

namespace HRM.Modules.Identity.Domain.Entities;

/// <summary>
/// Role aggregate root
/// Represents a collection of permissions that can be assigned to users/operators
///
/// Role vs Permissions:
/// - Role: Named collection of permissions (e.g., "HR Manager", "System Admin")
/// - Permission: Specific action on entity (e.g., "Personnel.Employee.View")
///
/// Responsibilities:
/// - Permission collection management (add, remove, update)
/// - Duplicate validation (fail-fast strategy)
/// - Business rule enforcement
/// - Audit trail (via Entity base class)
///
/// Business Rules:
/// - Role name must be unique (enforced at application layer)
/// - Role must have at least one permission
/// - No duplicate permissions allowed (fail-fast validation)
/// - Cannot delete role with active user assignments (enforced at application layer)
///
/// Validation Strategy: FAIL-FAST
/// - Immediately reject duplicate permissions
/// - No automatic conflict resolution
/// - No silent failures
/// - Clear error messages for user action
///
/// Domain Events:
/// - RoleCreatedDomainEvent: Raised when new role created
/// - RoleUpdatedDomainEvent: Raised when role name/description updated
/// - RolePermissionsModifiedDomainEvent: Raised when permissions added/removed
/// - RoleDeletedDomainEvent: Raised when role soft-deleted
/// </summary>
public sealed class Role : Entity, IAggregateRoot
{
    private readonly List<RolePermission> _permissions = new();

    /// <summary>
    /// Unique role name (e.g., "System Administrator", "HR Manager")
    /// Must be unique across all roles
    /// 3-100 characters
    /// </summary>
    public string Name { get; private set; } = default!;

    /// <summary>
    /// Optional description of role purpose and responsibilities
    /// Helps admins understand when to assign this role
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Whether this role is for operators (true) or users (false)
    ///
    /// Operator Roles:
    /// - Global access without scope restrictions
    /// - For internal staff/admins
    /// - Example: "System Administrator", "Support Engineer"
    ///
    /// User Roles:
    /// - Scope-based access (Company, Department, Position, Self)
    /// - For end users
    /// - Example: "HR Manager", "Employee Self-Service"
    /// </summary>
    public bool IsOperatorRole { get; private set; }

    /// <summary>
    /// Read-only collection of permissions assigned to this role
    /// Permissions cannot be modified directly - use AddPermission/RemovePermission methods
    /// </summary>
    public IReadOnlyCollection<RolePermission> Permissions => _permissions.AsReadOnly();

    /// <summary>
    /// Number of permissions in this role
    /// Used for validation and reporting
    /// </summary>
    public int PermissionCount => _permissions.Count;

    /// <summary>
    /// Private parameterless constructor for EF Core
    /// </summary>
    private Role()
    {
    }

    /// <summary>
    /// Factory method to create a new role
    /// Creates empty role that requires permissions to be added
    ///
    /// Business Rules:
    /// - Name must be unique (validated at application layer)
    /// - Name must be 3-100 characters
    /// - Role must have at least one permission before saving (validated at application layer)
    ///
    /// Domain Event:
    /// - Raises RoleCreatedDomainEvent after permissions are added
    /// </summary>
    /// <param name="name">Unique role name (3-100 chars)</param>
    /// <param name="description">Optional description</param>
    /// <param name="isOperatorRole">True for operator role, false for user role</param>
    /// <returns>New empty role</returns>
    /// <exception cref="ArgumentException">If name is invalid</exception>
    public static Role Create(string name, string? description = null, bool isOperatorRole = false)
    {
        ValidateName(name);

        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Description = description?.Trim(),
            IsOperatorRole = isOperatorRole
        };

        return role;
    }

    /// <summary>
    /// Add a permission to this role
    ///
    /// FAIL-FAST VALIDATION:
    /// - Immediately throws if permission already exists
    /// - No duplicate removal
    /// - No silent skipping
    /// - User must handle the conflict
    ///
    /// Duplicate Detection:
    /// Two permissions are duplicates if they have the same:
    /// - Module
    /// - Entity
    /// - Action
    /// - Scope (or both are null)
    ///
    /// Examples of duplicates:
    /// - Personnel.Employee.View (Department) + Personnel.Employee.View (Department)
    /// - System.Configuration.Update + System.Configuration.Update
    ///
    /// Examples of non-duplicates:
    /// - Personnel.Employee.View (Department) + Personnel.Employee.View (Company)
    /// - Personnel.Employee.View + Personnel.Employee.Create
    /// </summary>
    /// <param name="permission">Permission to add</param>
    /// <exception cref="InvalidOperationException">If permission already exists (FAIL-FAST)</exception>
    public void AddPermission(RolePermission permission)
    {
        // FAIL-FAST: Check for duplicate
        if (HasPermission(permission))
        {
            throw new InvalidOperationException(
                $"Duplicate permission detected: {permission}. " +
                $"This permission is already assigned to role '{Name}'. " +
                $"Please remove the duplicate or choose a different permission."
            );
        }

        _permissions.Add(permission);
    }

    /// <summary>
    /// Add multiple permissions to this role
    ///
    /// FAIL-FAST VALIDATION:
    /// - Stops immediately on first duplicate
    /// - No partial adds
    /// - All permissions must be unique
    /// - Validates against existing permissions AND within the new collection
    ///
    /// Behavior:
    /// - If any permission is duplicate, throws exception and adds NONE
    /// - Transaction-safe: either all succeed or none
    /// </summary>
    /// <param name="permissions">Permissions to add</param>
    /// <exception cref="InvalidOperationException">If any duplicate found (FAIL-FAST)</exception>
    /// <exception cref="ArgumentException">If permissions collection is null or empty</exception>
    public void AddPermissions(IEnumerable<RolePermission> permissions)
    {
        if (permissions == null)
            throw new ArgumentNullException(nameof(permissions));

        var permissionList = permissions.ToList();
        if (permissionList.Count == 0)
            throw new ArgumentException("Cannot add empty permission collection", nameof(permissions));

        // FAIL-FAST: Check for duplicates within the new collection first
        var duplicatesInNewCollection = permissionList
            .GroupBy(p => p)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicatesInNewCollection.Any())
        {
            var duplicatesList = string.Join(", ", duplicatesInNewCollection.Select(p => p.ToString()));
            throw new InvalidOperationException(
                $"Duplicate permissions found in the collection being added: {duplicatesList}. " +
                $"Please remove duplicates and try again."
            );
        }

        // FAIL-FAST: Check for duplicates with existing permissions
        foreach (var permission in permissionList)
        {
            if (HasPermission(permission))
            {
                throw new InvalidOperationException(
                    $"Duplicate permission detected: {permission}. " +
                    $"This permission is already assigned to role '{Name}'. " +
                    $"Operation cancelled - no permissions were added."
                );
            }
        }

        // All validations passed - add all permissions
        _permissions.AddRange(permissionList);
    }

    /// <summary>
    /// Remove a permission from this role
    ///
    /// Business Rules:
    /// - Role must have at least one permission (validated when count would be 0)
    /// - If removing last permission, throws exception
    /// </summary>
    /// <param name="permission">Permission to remove</param>
    /// <exception cref="InvalidOperationException">If permission not found or would leave role empty</exception>
    public void RemovePermission(RolePermission permission)
    {
        if (_permissions.Count == 1)
        {
            throw new InvalidOperationException(
                $"Cannot remove last permission from role '{Name}'. " +
                $"Role must have at least one permission."
            );
        }

        var removed = _permissions.Remove(permission);
        if (!removed)
        {
            throw new InvalidOperationException(
                $"Permission {permission} not found in role '{Name}'"
            );
        }
    }

    /// <summary>
    /// Remove multiple permissions from this role
    ///
    /// Business Rules:
    /// - Role must have at least one permission after removal
    /// - All permissions must exist in role
    /// - Transaction-safe: either all removed or none
    /// </summary>
    /// <param name="permissions">Permissions to remove</param>
    /// <exception cref="InvalidOperationException">If would leave role empty or permission not found</exception>
    public void RemovePermissions(IEnumerable<RolePermission> permissions)
    {
        if (permissions == null)
            throw new ArgumentNullException(nameof(permissions));

        var permissionList = permissions.ToList();
        if (permissionList.Count == 0)
            return; // Nothing to remove

        // Validate: Check if removal would leave role empty
        if (_permissions.Count - permissionList.Count < 1)
        {
            throw new InvalidOperationException(
                $"Cannot remove {permissionList.Count} permission(s) from role '{Name}'. " +
                $"Role must have at least one permission. Current count: {_permissions.Count}"
            );
        }

        // Validate: Check all permissions exist
        foreach (var permission in permissionList)
        {
            if (!HasPermission(permission))
            {
                throw new InvalidOperationException(
                    $"Permission {permission} not found in role '{Name}'. " +
                    $"Operation cancelled - no permissions were removed."
                );
            }
        }

        // All validations passed - remove all permissions
        foreach (var permission in permissionList)
        {
            _permissions.Remove(permission);
        }
    }

    /// <summary>
    /// Clear all permissions and set new collection
    /// Used for bulk updates where easier to replace than modify
    ///
    /// FAIL-FAST VALIDATION:
    /// - All new permissions must be unique
    /// - Must have at least one permission
    /// </summary>
    /// <param name="permissions">New permissions to set</param>
    /// <exception cref="InvalidOperationException">If duplicates found</exception>
    /// <exception cref="ArgumentException">If empty collection</exception>
    public void SetPermissions(IEnumerable<RolePermission> permissions)
    {
        if (permissions == null)
            throw new ArgumentNullException(nameof(permissions));

        var permissionList = permissions.ToList();
        if (permissionList.Count == 0)
            throw new ArgumentException("Role must have at least one permission", nameof(permissions));

        // FAIL-FAST: Check for duplicates in new collection
        var duplicates = permissionList
            .GroupBy(p => p)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Any())
        {
            var duplicatesList = string.Join(", ", duplicates.Select(p => p.ToString()));
            throw new InvalidOperationException(
                $"Duplicate permissions found: {duplicatesList}. " +
                $"Please remove duplicates and try again."
            );
        }

        var oldCount = _permissions.Count;
        _permissions.Clear();
        _permissions.AddRange(permissionList);

        // Raise event for permission changes
        AddDomainEvent(new RolePermissionsModifiedDomainEvent(
            Id,
            Name,
            PermissionsAdded: permissionList.Count,
            PermissionsRemoved: oldCount,
            TotalPermissions: _permissions.Count
        ));
    }

    /// <summary>
    /// Check if role has specific permission
    /// Uses record equality (compares all properties: Module, Entity, Action, Scope)
    /// </summary>
    public bool HasPermission(RolePermission permission) =>
        _permissions.Contains(permission);

    /// <summary>
    /// Check if role has permission with specific key (ignoring scope)
    /// Useful for checking if user can perform action regardless of scope
    /// Example: HasPermissionKey("Personnel.Employee.View") returns true for any scope
    /// </summary>
    public bool HasPermissionKey(string permissionKey) =>
        _permissions.Any(p => p.PermissionKey == permissionKey);

    /// <summary>
    /// Get all permissions for specific module
    /// Useful for UI grouping and filtering
    /// </summary>
    public IEnumerable<RolePermission> GetPermissionsByModule(string module) =>
        _permissions.Where(p => p.Module.Equals(module, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Get all unique modules in this role
    /// Useful for UI display
    /// </summary>
    public IEnumerable<string> GetModules() =>
        _permissions.Select(p => p.Module).Distinct();

    /// <summary>
    /// Update role name and description
    ///
    /// Business Rules:
    /// - Name must be unique (validated at application layer)
    /// - Name must be 3-100 characters
    ///
    /// Domain Event:
    /// - Raises RoleUpdatedDomainEvent
    /// </summary>
    /// <param name="name">New role name</param>
    /// <param name="description">New description</param>
    /// <exception cref="ArgumentException">If name is invalid</exception>
    public void Update(string name, string? description = null)
    {
        ValidateName(name);

        Name = name.Trim();
        Description = description?.Trim();

        AddDomainEvent(new RoleUpdatedDomainEvent(Id, Name, PermissionCount));
    }

    /// <summary>
    /// Mark role for deletion (soft delete)
    ///
    /// Business Rules:
    /// - Cannot delete role with active user assignments (validated at application layer)
    ///
    /// Domain Event:
    /// - Raises RoleDeletedDomainEvent
    /// </summary>
    public override void Delete()
    {
        base.Delete();
        AddDomainEvent(new RoleDeletedDomainEvent(Id, Name));
    }

    /// <summary>
    /// Finalize role creation after permissions are added
    /// Call this after adding all initial permissions
    /// Raises RoleCreatedDomainEvent
    /// </summary>
    /// <exception cref="InvalidOperationException">If role has no permissions</exception>
    public void FinalizeCreation()
    {
        if (_permissions.Count == 0)
        {
            throw new InvalidOperationException(
                $"Cannot finalize role '{Name}' without any permissions. " +
                $"Please add at least one permission before finalizing."
            );
        }

        AddDomainEvent(new RoleCreatedDomainEvent(
            Id,
            Name,
            Description,
            PermissionCount
        ));
    }

    /// <summary>
    /// Validate role name format and length
    /// </summary>
    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Role name cannot be empty", nameof(name));

        var trimmedName = name.Trim();
        if (trimmedName.Length < 3)
            throw new ArgumentException("Role name must be at least 3 characters", nameof(name));

        if (trimmedName.Length > 100)
            throw new ArgumentException("Role name cannot exceed 100 characters", nameof(name));
    }
}
