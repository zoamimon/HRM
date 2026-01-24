namespace HRM.Modules.Identity.Domain.Enums;

/// <summary>
/// Defines which user types a permission template can be assigned to
/// Controls template visibility and assignment in UI
/// </summary>
public enum ApplicableTo
{
    /// <summary>
    /// Template can only be assigned to Users (employees)
    /// Users have scope-based access (Company, Department, Position, Employee)
    ///
    /// UI Behavior:
    /// - Show in User permission assignment screen
    /// - Display scope selection dropdown (mandatory)
    /// - Hide from Operator permission assignment
    ///
    /// Example Templates:
    /// - Employee Self-Service (scope: Employee)
    /// - Department Manager (scope: Department)
    /// - HR Manager (scope: Company)
    /// </summary>
    User = 1,

    /// <summary>
    /// Template can only be assigned to Operators (system admins)
    /// Operators have global access without scope restrictions
    ///
    /// UI Behavior:
    /// - Show in Operator permission assignment screen
    /// - Hide scope selection (operators don't have scopes)
    /// - Hide from User permission assignment
    ///
    /// Example Templates:
    /// - System Administrator (full access)
    /// - IT Support (technical operations)
    /// - Auditor (read-only global access)
    /// </summary>
    Operator = 2,

    /// <summary>
    /// Template can be assigned to both Users and Operators
    /// Flexible template that adapts based on assignee type
    ///
    /// UI Behavior:
    /// - Show in both User and Operator assignment screens
    /// - Show scope selection only when assigning to User
    /// - Hide scope selection when assigning to Operator
    ///
    /// Example Templates:
    /// - Viewer (read-only access for anyone)
    /// - Data Entry (basic CRUD for both types)
    /// - Reporter (generate reports with appropriate scope)
    ///
    /// Runtime Behavior:
    /// - When assigned to User → scope is mandatory and enforced
    /// - When assigned to Operator → scope is ignored, full access granted
    /// </summary>
    Both = 3
}
