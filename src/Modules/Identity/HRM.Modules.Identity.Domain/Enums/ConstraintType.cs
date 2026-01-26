namespace HRM.Modules.Identity.Domain.Enums;

/// <summary>
/// Defines types of permission constraints that can be applied to actions
/// Constraints are additional conditions that must be met for permission to be granted
/// </summary>
public enum ConstraintType
{
    /// <summary>
    /// Requires the current user to be the manager of the target employee
    /// Used for hierarchical permissions (e.g., manager can update subordinate's data)
    ///
    /// Example Use Cases:
    /// - Manager approving subordinate's leave requests
    /// - Manager updating subordinate's performance review
    /// - Manager viewing subordinate's salary information
    ///
    /// Evaluation Logic:
    /// - Check if CurrentUserId exists in EmployeeAssignments.ManagerId for TargetEmployeeId
    /// - Can support indirect management (manager of manager) with MaxLevels parameter
    /// </summary>
    ManagerOfTarget = 1,

    /// <summary>
    /// Restricts access to specific fields within an entity
    /// Used for field-level security (e.g., hide salary field for non-HR users)
    ///
    /// Example Use Cases:
    /// - Hide Salary and Bonus fields from regular employees
    /// - Restrict BirthDate to HR department only
    /// - Show only public fields to external users
    ///
    /// Evaluation Logic:
    /// - Maintain a list of restricted fields in constraint parameters
    /// - Filter fields in query results (SELECT only allowed fields)
    /// - Block updates to restricted fields
    /// </summary>
    FieldRestriction = 2,

    /// <summary>
    /// Restricts actions based on date range
    /// Used for time-based permissions (e.g., only edit records from last 30 days)
    ///
    /// Example Use Cases:
    /// - Allow editing attendance records only within current month
    /// - Restrict leave requests to future dates only
    /// - Limit access to historical data older than 1 year
    ///
    /// Evaluation Logic:
    /// - Check if target entity's relevant date field falls within allowed range
    /// - Range can be absolute (2024-01-01 to 2024-12-31) or relative (last 30 days)
    /// </summary>
    DateRange = 3,

    /// <summary>
    /// Restricts actions based on workflow state
    /// Used for state machine permissions (e.g., only approve pending requests)
    ///
    /// Example Use Cases:
    /// - Only approve leave requests in "Pending" state
    /// - Cannot edit employee records in "Terminated" status
    /// - Only submit timesheets in "Draft" state
    ///
    /// Evaluation Logic:
    /// - Check if target entity's status/state field matches allowed states
    /// - Different actions may require different states
    /// </summary>
    WorkflowState = 4,

    /// <summary>
    /// Restricts actions based on custom business rules
    /// Used for complex conditions that don't fit other constraint types
    ///
    /// Example Use Cases:
    /// - Allow editing only if total amount is below threshold
    /// - Require approval if overtime hours exceed 10
    /// - Block deletion if entity has child records
    ///
    /// Evaluation Logic:
    /// - Execute custom validation logic defined in constraint parameters
    /// - Can invoke external services or complex queries
    /// </summary>
    CustomRule = 99
}
