namespace HRM.BuildingBlocks.Domain.Enums;

/// <summary>
/// Defines the data visibility scope for employee users (UserType.User)
/// Used for authorization and data filtering in queries
/// Determines what data a user can see and manage
/// 
/// Database Storage:
/// - Stored as INT in Users table (1, 2, 3, 4)
/// - Column: [identity].[Users].[ScopeLevel]
/// 
/// JWT Token Usage:
/// - Claim name: "ScopeLevel"
/// - Claim value: "Company", "Department", "Position", or "Employee"
/// - Used in data scoping service and authorization
/// 
/// Important Notes:
/// - Does NOT apply to Operators (they have global access)
/// - Only applies to Users (UserType.User)
/// - Determined by position/role at user creation
/// - Can be changed by System Admin (triggers session revoke)
/// </summary>
public enum ScopeLevel
{
    /// <summary>
    /// Company-level access
    /// User can view and manage data within their assigned companies
    /// 
    /// Who Gets This Level:
    /// - Company CEO
    /// - General Manager
    /// - Company-wide administrators
    /// - Board members
    /// 
    /// Data Access Rules:
    /// ✅ All employees in assigned companies
    /// ✅ All departments in assigned companies
    /// ✅ All positions in assigned companies
    /// ✅ All organizational data in assigned companies
    /// ❌ Employees in other companies
    /// ❌ Data from companies not assigned to user
    /// 
    /// Assignment Logic:
    /// - User is assigned to Company A and Company B via EmployeeAssignments
    /// - Can see all data in Company A and Company B
    /// - Cannot see data in Company C
    /// 
    /// Query Filter Example:
    /// WHERE ea.CompanyId IN (CompanyA, CompanyB)
    /// 
    /// Use Cases:
    /// - CEO viewing all company employees
    /// - General Manager managing all departments
    /// - Company Admin configuring company settings
    /// - Multi-company executive (works in 2+ companies)
    /// </summary>
    Company = 1,

    /// <summary>
    /// Department-level access
    /// User can view and manage data within their assigned departments
    /// 
    /// Who Gets This Level:
    /// - Department Managers
    /// - Department Heads
    /// - Department Administrators
    /// - HR Managers (for HR department)
    /// 
    /// Data Access Rules:
    /// ✅ All employees in assigned departments
    /// ✅ All positions in assigned departments
    /// ✅ Department details and settings
    /// ✅ Sub-departments (if hierarchical)
    /// ❌ Employees in other departments (even in same company)
    /// ❌ Company-wide data
    /// 
    /// Assignment Logic:
    /// - User is assigned to IT Department and HR Department via EmployeeAssignments
    /// - Can see all employees in IT and HR departments
    /// - Cannot see Finance Department employees (same company!)
    /// 
    /// Query Filter Example:
    /// WHERE ea.DepartmentId IN (IT_Dept, HR_Dept)
    /// 
    /// Use Cases:
    /// - IT Manager viewing all IT staff
    /// - HR Manager managing HR department
    /// - Department Head reviewing department performance
    /// - Cross-department manager (manages 2+ departments)
    /// </summary>
    Department = 2,

    /// <summary>
    /// Position-level access (Team level)
    /// User can view and manage data for employees with the same position
    /// 
    /// Who Gets This Level:
    /// - Team Leads
    /// - Senior positions leading juniors in same role
    /// - Project Managers (for project team)
    /// - Scrum Masters
    /// 
    /// Data Access Rules:
    /// ✅ Employees with same positions (in same department)
    /// ✅ Position details and responsibilities
    /// ✅ Team member information
    /// ❌ Employees with different positions
    /// ❌ Other departments
    /// ❌ Company-wide data
    /// 
    /// Assignment Logic:
    /// - User is "Senior Developer" in IT Department
    /// - Can see other "Senior Developers" in IT Department
    /// - Cannot see "Junior Developers" or "QA Engineers"
    /// 
    /// Query Filter Example:
    /// WHERE ea.PositionId IN (Senior_Dev_Position)
    ///   AND ea.DepartmentId = IT_Dept
    /// 
    /// Use Cases:
    /// - Senior Developer mentoring other seniors
    /// - Team Lead viewing team members with same role
    /// - Project Manager seeing project team members
    /// - Position-specific access (not full department)
    /// 
    /// Note: This is TEAM level, not individual level
    /// </summary>
    Position = 3,

    /// <summary>
    /// Employee-level access (Self-service only)
    /// User can only view and manage their own data
    /// 
    /// Who Gets This Level:
    /// - Regular employees (default level)
    /// - Staff members
    /// - Individual contributors
    /// - Most employees in the system
    /// 
    /// Data Access Rules:
    /// ✅ Own employee profile
    /// ✅ Own attendance records
    /// ✅ Own leave requests
    /// ✅ Own salary information (if permitted)
    /// ✅ Own performance reviews
    /// ❌ Cannot see other employees
    /// ❌ Cannot see organizational structure
    /// ❌ Cannot see department data
    /// ❌ Cannot see company-wide reports
    /// 
    /// Assignment Logic:
    /// - User.Id = Employee.Id (same GUID)
    /// - Filter: WHERE e.Id = CurrentUserId
    /// 
    /// Query Filter Example:
    /// WHERE e.Id = @CurrentUserId
    /// 
    /// Use Cases:
    /// - Employee viewing own profile
    /// - Staff updating own information
    /// - Employee submitting leave requests
    /// - Self-service portal access
    /// - Basic employee portal
    /// 
    /// Default Level:
    /// - This is the DEFAULT ScopeLevel when creating a User
    /// - Most restrictive level
    /// - Can be upgraded to higher levels by Admin
    /// </summary>
    Employee = 4
}
