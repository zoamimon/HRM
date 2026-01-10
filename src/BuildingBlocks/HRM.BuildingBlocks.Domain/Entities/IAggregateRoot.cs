namespace HRM.BuildingBlocks.Domain.Entities;

/// <summary>
/// Marker interface for aggregate roots in Domain-Driven Design
/// 
/// An aggregate is a cluster of domain objects that can be treated as a single unit
/// The aggregate root is the only member of the aggregate that outside objects are allowed to hold references to
/// 
/// Key Principles:
/// 1. Only aggregate roots can be directly queried from repositories
/// 2. Changes to the aggregate must go through the root
/// 3. Transactions should not span multiple aggregates
/// 4. Use domain events for cross-aggregate communication
/// 
/// Examples in HRM System:
/// 
/// Simple Aggregates (no child entities):
/// - Operator (aggregate root only)
/// - Role (aggregate root only)
/// - Company (aggregate root only)
/// 
/// Complex Aggregates (with child entities):
/// - User (root) → UserRoles (children)
/// - Employee (root) → EmployeeAssignments (children)
/// - Department (root) → Child Departments (tree structure)
/// 
/// Incorrect Examples (don't make these aggregate roots):
/// - UserRole (child of User)
/// - EmployeeAssignment (child of Employee)
/// - RefreshToken (child of User/Operator)
/// 
/// Transaction Boundaries:
/// - ✅ Create User + Assign Roles (same aggregate)
/// - ✅ Create Employee + Create Assignments (same aggregate)
/// - ❌ Create Employee + Create User (different aggregates → use domain events)
/// - ❌ Update Employee + Update Department (different aggregates → separate transactions)
/// </summary>
public interface IAggregateRoot
{
    // Marker interface - no methods required
    // The presence of this interface indicates that the entity is an aggregate root
    // Used by repositories to ensure only aggregate roots are directly persisted
}
