using HRM.BuildingBlocks.Domain.Enums;

namespace HRM.BuildingBlocks.Application.Abstractions.Authentication;

/// <summary>
/// Service for accessing current authenticated user information.
/// Provides strongly-typed access to JWT claims without coupling to HTTP infrastructure.
/// 
/// Clean Architecture Compliance:
/// - Application layer defines interface (this file)
/// - Infrastructure layer implements (reads from HttpContext.User claims)
/// - No direct dependency on ASP.NET Core in application layer
/// 
/// Implementation Location:
/// Infrastructure layer implementation reads from:
/// - HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)
/// - HttpContext.User.FindFirst("UserType")
/// - HttpContext.User.FindFirst("ScopeLevel")
/// - etc.
/// 
/// JWT Token Structure:
/// <code>
/// {
///   "sub": "user-guid",              // UserId
///   "name": "john.doe",              // Username
///   "email": "john@example.com",     // Email
///   "UserType": "User",              // Operator or User
///   "ScopeLevel": "Department",      // Only for UserType=User
///   "EmployeeId": "employee-guid",   // Only for UserType=User
///   "Roles": "Manager,HR",           // Comma-separated roles
///   "jti": "token-id",
///   "exp": 1234567890,
///   "iss": "HRM.Api",
///   "aud": "HRM.Clients"
/// }
/// </code>
/// 
/// Usage in Command/Query Handlers:
/// <code>
/// public class CreateDepartmentCommandHandler : ICommandHandler&lt;CreateDepartmentCommand, Guid&gt;
/// {
///     private readonly ICurrentUserService _currentUser;
///     
///     public async Task&lt;Result&lt;Guid&gt;&gt; Handle(...)
///     {
///         // Get current user ID for audit trail
///         var currentUserId = _currentUser.UserId;
///         
///         // Check if user is Operator (global access)
///         if (_currentUser.UserType == UserType.Operator)
///         {
///             // Allow operation
///         }
///         
///         // Check user's scope level
///         if (_currentUser.ScopeLevel == ScopeLevel.Company)
///         {
///             // Company-level user can create departments
///         }
///         
///         // Create department with audit info
///         var department = Department.Create(
///             command.Name,
///             command.CompanyId,
///             currentUserId // CreatedBy
///         );
///     }
/// }
/// </code>
/// 
/// Authorization Scenarios:
/// 
/// 1. Operator Access (Global):
/// <code>
/// if (_currentUser.UserType == UserType.Operator)
/// {
///     // Operators can access all data without scoping
///     // No need to check ScopeLevel or EmployeeId
/// }
/// </code>
/// 
/// 2. User Access (Scoped):
/// <code>
/// if (_currentUser.UserType == UserType.User)
/// {
///     // Check scope level
///     switch (_currentUser.ScopeLevel)
///     {
///         case ScopeLevel.Company:
///             // Can access all data in assigned companies
///             break;
///         case ScopeLevel.Department:
///             // Can access data in assigned departments
///             break;
///         case ScopeLevel.Position:
///             // Can access data for same position
///             break;
///         case ScopeLevel.Employee:
///             // Can only access own data
///             if (requestedEmployeeId != _currentUser.EmployeeId)
///                 return Result.Failure(Error.Forbidden(...));
///             break;
///     }
/// }
/// </code>
/// 
/// Anonymous Access Handling:
/// - IsAuthenticated returns false for anonymous requests
/// - UserId throws InvalidOperationException if not authenticated
/// - Always check IsAuthenticated before accessing other properties
/// 
/// Thread Safety:
/// - HttpContext is scoped per request (thread-safe)
/// - Each request gets its own ICurrentUserService instance
/// - No shared state between requests
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Gets the current user's unique identifier (from JWT 'sub' claim).
    /// For Operators: OperatorId
    /// For Users: UserId (same as EmployeeId)
    /// 
    /// Throws InvalidOperationException if accessed when not authenticated.
    /// Always check IsAuthenticated first!
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when user is not authenticated</exception>
    Guid UserId { get; }

    /// <summary>
    /// Gets the current user's username (from JWT 'name' claim).
    /// Used for audit logs and display purposes.
    /// </summary>
    string? Username { get; }

    /// <summary>
    /// Gets the current user's email (from JWT 'email' claim).
    /// Used for notifications and audit purposes.
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// Gets the current user's type (Operator or User).
    /// 
    /// Critical for authorization:
    /// - Operator: Global access, no data scoping
    /// - User: Scoped access based on ScopeLevel and assignments
    /// </summary>
    UserType UserType { get; }

    /// <summary>
    /// Gets the current user's scope level (only for UserType = User).
    /// Null for Operators (they have global access).
    /// 
    /// Determines data visibility:
    /// - Company: See all data in assigned companies
    /// - Department: See data in assigned departments
    /// - Position: See team members with same position
    /// - Employee: See only own data
    /// </summary>
    ScopeLevel? ScopeLevel { get; }

    /// <summary>
    /// Gets the current user's employee ID (only for UserType = User).
    /// Null for Operators.
    /// 
    /// Important: For Users, UserId == EmployeeId (same value)
    /// Used for querying employee assignments and data scoping.
    /// </summary>
    Guid? EmployeeId { get; }

    /// <summary>
    /// Gets the current user's roles (from JWT 'Roles' claim).
    /// Returns read-only collection of roles parsed from comma-separated string.
    ///
    /// Example: "Manager,HR,DepartmentHead" → ["Manager", "HR", "DepartmentHead"]
    ///
    /// Important:
    /// - Never returns null (returns empty collection if no roles)
    /// - Use IsInRole() or HasRole() for checking role membership
    /// - Collection is read-only to prevent modification
    ///
    /// Used for role-based authorization checks.
    /// </summary>
    IReadOnlyCollection<string> Roles { get; }

    /// <summary>
    /// Indicates whether the current request is authenticated.
    /// 
    /// IMPORTANT: Always check this before accessing other properties!
    /// 
    /// Best Practice:
    /// <code>
    /// if (!_currentUser.IsAuthenticated)
    /// {
    ///     return Result.Failure(
    ///         Error.Unauthorized("Auth.Required", "Authentication required")
    ///     );
    /// }
    /// 
    /// var userId = _currentUser.UserId; // Safe to access now
    /// </code>
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Checks if the current user has a specific role.
    /// Case-insensitive comparison.
    ///
    /// Usage:
    /// <code>
    /// if (_currentUser.HasRole("SystemAdmin"))
    /// {
    ///     // Allow sensitive operation
    /// }
    ///
    /// if (_currentUser.HasRole("Manager"))
    /// {
    ///     // Allow management operations
    /// }
    /// </code>
    /// </summary>
    /// <param name="role">Role name to check</param>
    /// <returns>True if user has the role, false otherwise</returns>
    bool HasRole(string role);

    /// <summary>
    /// Checks if the current user has a specific role.
    /// Case-insensitive comparison (alias for HasRole).
    ///
    /// Cleaner syntax for role checks in Application layer.
    ///
    /// Usage:
    /// <code>
    /// if (_currentUser.IsInRole("Admin"))
    /// {
    ///     // Allow admin operation
    /// }
    ///
    /// if (_currentUser.IsInRole("Manager"))
    /// {
    ///     // Allow manager operation
    /// }
    /// </code>
    /// </summary>
    /// <param name="role">Role name to check (case-insensitive)</param>
    /// <returns>True if user has the role, false otherwise</returns>
    bool IsInRole(string role);

    /// <summary>
    /// Checks if the current user is an Operator.
    /// Convenience method for common authorization check.
    /// 
    /// Usage:
    /// <code>
    /// if (_currentUser.IsOperator())
    /// {
    ///     // Operators have global access
    ///     // No data scoping needed
    /// }
    /// else
    /// {
    ///     // Users need data scoping applied
    ///     await _dataScopingService.ApplyScopingAsync(query);
    /// }
    /// </code>
    /// </summary>
    /// <returns>True if user is an Operator, false otherwise</returns>
    bool IsOperator();

    /// <summary>
    /// Checks if the current user is a regular User (employee).
    /// Convenience method for common authorization check.
    /// 
    /// Usage:
    /// <code>
    /// if (_currentUser.IsUser())
    /// {
    ///     // Apply data scoping based on ScopeLevel
    ///     var scopeContext = await _dataScopingService.GetCurrentScopeAsync();
    /// }
    /// </code>
    /// </summary>
    /// <returns>True if user is a User (employee), false otherwise</returns>
    bool IsUser();
}
