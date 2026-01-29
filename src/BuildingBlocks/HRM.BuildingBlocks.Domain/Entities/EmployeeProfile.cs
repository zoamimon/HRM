using HRM.BuildingBlocks.Domain.Abstractions.Audit;
using HRM.BuildingBlocks.Domain.Enums;

namespace HRM.BuildingBlocks.Domain.Entities;

/// <summary>
/// Profile data for Employee accounts
/// Links Account to Employee entity and contains scope information
///
/// Relationship:
/// - Account (1) --- (0..1) EmployeeProfile
/// - EmployeeProfile (1) --- (1) Employee (in Personnel module)
/// - Only for AccountType.Employee
///
/// This bridges authentication (Account) with HR data (Employee).
/// </summary>
public class EmployeeProfile : Entity, IAuditableEntity
{
    /// <summary>
    /// Reference to the Account
    /// </summary>
    public Guid AccountId { get; private set; }

    /// <summary>
    /// Reference to the Employee entity (in Personnel module)
    /// </summary>
    public Guid EmployeeId { get; private set; }

    /// <summary>
    /// Default scope level for this employee's permissions
    /// Can be overridden per-permission via RolePermissions
    /// </summary>
    public ScopeLevel DefaultScopeLevel { get; private set; } = ScopeLevel.Employee;

    /// <summary>
    /// Primary company ID (for multi-company scenarios)
    /// </summary>
    public Guid? PrimaryCompanyId { get; private set; }

    /// <summary>
    /// Primary department ID
    /// </summary>
    public Guid? PrimaryDepartmentId { get; private set; }

    /// <summary>
    /// Primary position ID
    /// </summary>
    public Guid? PrimaryPositionId { get; private set; }

    /// <summary>
    /// Whether employee can access data across all assigned companies
    /// (vs only primary company)
    /// </summary>
    public bool CanAccessAllAssignedCompanies { get; private set; } = true;

    // Audit fields inherited from Entity base class:
    // - CreatedAtUtc, ModifiedAtUtc
    // - CreatedById, ModifiedById (Guid?)

    // Navigation property (configured in EF)
    // public Account Account { get; private set; } = null!;

    // Private constructor for EF
    private EmployeeProfile() { }

    /// <summary>
    /// Create a new employee profile
    /// </summary>
    public static EmployeeProfile Create(
        Guid accountId,
        Guid employeeId,
        ScopeLevel defaultScopeLevel = ScopeLevel.Employee,
        Guid? primaryCompanyId = null,
        Guid? primaryDepartmentId = null,
        Guid? primaryPositionId = null)
    {
        return new EmployeeProfile
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            EmployeeId = employeeId,
            DefaultScopeLevel = defaultScopeLevel,
            PrimaryCompanyId = primaryCompanyId,
            PrimaryDepartmentId = primaryDepartmentId,
            PrimaryPositionId = primaryPositionId
            // CreatedAtUtc is set automatically by Entity base class constructor
        };
    }

    /// <summary>
    /// Update primary assignments
    /// </summary>
    public void UpdatePrimaryAssignments(
        Guid? companyId,
        Guid? departmentId,
        Guid? positionId)
    {
        PrimaryCompanyId = companyId;
        PrimaryDepartmentId = departmentId;
        PrimaryPositionId = positionId;
        MarkAsModified();
    }

    /// <summary>
    /// Update default scope level
    /// </summary>
    public void UpdateDefaultScopeLevel(ScopeLevel scopeLevel)
    {
        DefaultScopeLevel = scopeLevel;
        MarkAsModified();
    }

    /// <summary>
    /// Set whether employee can access all assigned companies
    /// </summary>
    public void SetCanAccessAllAssignedCompanies(bool value)
    {
        CanAccessAllAssignedCompanies = value;
        MarkAsModified();
    }
}
