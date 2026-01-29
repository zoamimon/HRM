using HRM.BuildingBlocks.Domain.Abstractions.Audit;

namespace HRM.BuildingBlocks.Domain.Entities;

/// <summary>
/// Profile data for System accounts (operators, admins)
/// Contains role and permission information
///
/// Relationship:
/// - Account (1) --- (0..1) SystemProfile
/// - Only for AccountType.System
///
/// This separates system-specific data from the authentication entity.
/// </summary>
public class SystemProfile : Entity, IAuditableEntity
{
    /// <summary>
    /// Reference to the Account
    /// </summary>
    public Guid AccountId { get; private set; }

    /// <summary>
    /// Whether this is a super administrator (bypasses all permission checks)
    /// </summary>
    public bool IsSuperAdmin { get; private set; }

    /// <summary>
    /// Department/team this operator belongs to (optional)
    /// Used for organizational purposes, not scoping
    /// </summary>
    public string? Department { get; private set; }

    /// <summary>
    /// Job title (optional)
    /// </summary>
    public string? JobTitle { get; private set; }

    /// <summary>
    /// Notes about this operator (admin-only)
    /// </summary>
    public string? Notes { get; private set; }

    // Audit fields inherited from Entity base class:
    // - CreatedAtUtc, ModifiedAtUtc
    // - CreatedById, ModifiedById (Guid?)

    // Navigation property (configured in EF)
    // public Account Account { get; private set; } = null!;
    // public ICollection<SystemProfileRole> Roles { get; private set; } = new List<SystemProfileRole>();

    // Private constructor for EF
    private SystemProfile() { }

    /// <summary>
    /// Create a new system profile
    /// </summary>
    public static SystemProfile Create(
        Guid accountId,
        bool isSuperAdmin = false,
        string? department = null,
        string? jobTitle = null)
    {
        return new SystemProfile
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            IsSuperAdmin = isSuperAdmin,
            Department = department,
            JobTitle = jobTitle
            // CreatedAtUtc is set automatically by Entity base class constructor
        };
    }

    /// <summary>
    /// Grant super admin privileges
    /// </summary>
    public void GrantSuperAdmin()
    {
        IsSuperAdmin = true;
        MarkAsModified();
    }

    /// <summary>
    /// Revoke super admin privileges
    /// </summary>
    public void RevokeSuperAdmin()
    {
        IsSuperAdmin = false;
        MarkAsModified();
    }

    /// <summary>
    /// Update profile information
    /// </summary>
    public void Update(string? department, string? jobTitle, string? notes)
    {
        Department = department;
        JobTitle = jobTitle;
        Notes = notes;
        MarkAsModified();
    }
}
