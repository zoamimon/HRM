namespace HRM.BuildingBlocks.Domain.Enums;

/// <summary>
/// Defines the type of authenticated user in the system
/// Used for authorization, data scoping, and feature access
/// Stored in JWT token for authentication context
/// 
/// Database Storage:
/// - Stored as INT in database (1, 2)
/// - Mapped automatically by EF Core enum support
/// 
/// JWT Token Usage:
/// - Claim name: "UserType"
/// - Claim value: "Operator" or "User"
/// - Used in authorization policies
/// </summary>
public enum UserType
{
    /// <summary>
    /// System operator with global access
    /// 
    /// Characteristics:
    /// - Not linked to any employee
    /// - No ScopeLevel (global access)
    /// - Can manage all data across all companies, departments, positions
    /// - Cannot be an employee in the system
    /// 
    /// Use Cases:
    /// - System administrators
    /// - IT support staff
    /// - Super users
    /// - External consultants with full access
    /// 
    /// Data Scope:
    /// ✅ GLOBAL ACCESS to everything
    /// - All employees in all companies
    /// - All departments in all companies
    /// - All positions in all companies
    /// - All organizational data
    /// - System configuration
    /// 
    /// Example Operators:
    /// - admin@system.com (System Administrator)
    /// - it.support@company.com (IT Support)
    /// - superuser@company.com (Super User)
    /// 
    /// Table: [identity].[Operators]
    /// </summary>
    Operator = 1,

    /// <summary>
    /// Employee user with scoped access based on ScopeLevel
    /// 
    /// Characteristics:
    /// - Linked to an Employee entity (UserId = EmployeeId, same GUID!)
    /// - Has ScopeLevel (Company/Department/Position/Employee)
    /// - Can only access data within their assigned scope
    /// - Is an active employee in the organization
    /// 
    /// Use Cases:
    /// - Regular employees (self-service)
    /// - Department managers
    /// - Company executives
    /// - Team leads
    /// 
    /// Data Scope:
    /// ⚖️ SCOPED ACCESS based on ScopeLevel:
    /// - Company level: All data in assigned companies
    /// - Department level: All data in assigned departments
    /// - Position level: Only team members with same position
    /// - Employee level: Only own data
    /// 
    /// Example Users:
    /// - john.doe@company.com (Employee, ScopeLevel: Employee)
    /// - jane.manager@company.com (Manager, ScopeLevel: Department)
    /// - ceo@company.com (CEO, ScopeLevel: Company)
    /// 
    /// Table: [identity].[Users]
    /// Foreign Key: EmployeeId → [personnel].[Employees]
    /// </summary>
    User = 2
}
