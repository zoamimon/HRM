namespace HRM.BuildingBlocks.Domain.Enums;

/// <summary>
/// Scope level for authorization contracts.
/// Shared contract type used by BuildingBlocks authorization infrastructure
/// (RouteSecurityEntry, IPermissionService, etc.)
///
/// NOTE: This is a CONTRACT enum for shared authorization infrastructure.
/// The full ScopeLevel with business logic lives in Identity.Domain.Enums.
/// Both enums have identical values for serialization/deserialization compatibility.
///
/// Values (lower number = wider scope):
/// - Global (0): Access to all data across the system
/// - Company (1): Access limited to company-level data
/// - Department (2): Access limited to department-level data
/// - Position (3): Access limited to position-level data
/// - Employee (4): Access limited to own data only
/// </summary>
public enum ScopeLevel
{
    Global = 0,
    Company = 1,
    Department = 2,
    Position = 3,
    Employee = 4
}
