using HRM.BuildingBlocks.Domain.Enums;

namespace HRM.BuildingBlocks.Domain.Abstractions.Authentication;

/// <summary>
/// Interface for authenticatable entities (Operator and User)
/// Provides common contract for authentication and authorization
/// 
/// Purpose:
/// - Unified login endpoint for both Operators and Users
/// - Common JWT token generation logic
/// - Shared password verification
/// - Consistent authentication flow
/// 
/// Implementing Classes:
/// 
/// 1. Operator (Identity module):
///    - System administrators
///    - Global access (no ScopeLevel)
///    - Not linked to Employee
///    - Table: [identity].[Operators]
/// 
/// 2. User (Identity module):
///    - Employees with scoped access
///    - Has ScopeLevel (Company/Department/Position/Employee)
///    - Linked to Employee (UserId = EmployeeId)
///    - Table: [identity].[Users]
/// 
/// Login Flow Using This Interface:
/// 
/// 1. User submits username + password
/// 
/// 2. Search for authenticatable entity:
///    - First, search Operators table by username
///    - If not found, search Users table by username
///    - Cast result to IAuthenticatable
/// 
/// 3. Verify credentials:
///    - Get password hash: authenticatable.GetPasswordHash()
///    - Verify: passwordHasher.Verify(providedPassword, storedHash)
/// 
/// 4. Check if active:
///    - if (!authenticatable.GetIsActive()) return "Account disabled"
/// 
/// 5. Generate JWT token:
///    - Subject (sub): authenticatable.Id
///    - Name: authenticatable.GetUsername()
///    - Email: authenticatable.GetEmail()
///    - UserType: authenticatable.GetUserType() (Operator or User)
///    - ScopeLevel: Only if UserType = User (from User entity)
/// 
/// 6. Return access token + refresh token
/// 
/// Benefits:
/// - Single login endpoint handles both user types
/// - No code duplication in authentication logic
/// - Easy to extend (add new authenticatable types)
/// - Type-safe (compile-time checking)
/// - Testable (can mock interface)
/// </summary>
public interface IAuthenticatable
{
    /// <summary>
    /// Entity unique identifier
    /// 
    /// For Operator:
    /// - OperatorId (Guid)
    /// - Independent from Employee system
    /// 
    /// For User:
    /// - UserId (Guid)
    /// - IMPORTANT: UserId = EmployeeId (same GUID!)
    /// - Links User account to Employee record
    /// 
    /// Used in JWT token:
    /// - Claim name: "sub" (subject)
    /// - Claim value: Id.ToString()
    /// - Standard JWT claim for user identity
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Username for login
    /// 
    /// Requirements:
    /// - Must be unique across BOTH Operators AND Users tables
    /// - Cannot have same username in both tables
    /// - Validation enforced at application level
    /// 
    /// Format:
    /// - Usually: firstname.lastname
    /// - Or: email prefix (before @)
    /// - Lowercase recommended
    /// 
    /// Examples:
    /// - "admin" (Operator)
    /// - "john.doe" (User)
    /// - "jane.smith" (User)
    /// 
    /// Used in:
    /// - Login (find user by username)
    /// - JWT token (name claim)
    /// - Display in UI
    /// - Audit logs
    /// </summary>
    string GetUsername();

    /// <summary>
    /// Email address
    /// 
    /// Requirements:
    /// - Must be unique across BOTH Operators AND Users tables
    /// - Must be valid email format
    /// - Cannot have same email in both tables
    /// 
    /// Used for:
    /// - Password reset (send reset link to email)
    /// - Email notifications (welcome, alerts)
    /// - JWT token (email claim)
    /// - Two-factor authentication (send OTP)
    /// - Account recovery
    /// 
    /// Examples:
    /// - admin@system.com (Operator)
    /// - john.doe@company.com (User)
    /// </summary>
    string GetEmail();

    /// <summary>
    /// Hashed password (BCrypt or Argon2)
    /// 
    /// CRITICAL SECURITY RULES:
    /// - ❌ NEVER store plain text passwords!
    /// - ❌ NEVER log passwords (even hashed)!
    /// - ❌ NEVER return password hash in API responses!
    /// - ✅ ALWAYS use strong hashing (BCrypt/Argon2)
    /// - ✅ ALWAYS use unique salt per password (built-in to BCrypt/Argon2)
    /// 
    /// Hashing Algorithms:
    /// 
    /// BCrypt (Recommended):
    /// - Industry standard
    /// - Built-in salt
    /// - Adaptive (can increase work factor over time)
    /// - Resistant to rainbow tables
    /// - Work factor: 12 (good balance of security/performance)
    /// 
    /// Argon2 (More Secure):
    /// - Modern algorithm (won Password Hashing Competition 2015)
    /// - Memory-hard (resistant to GPU/ASIC attacks)
    /// - Configurable memory, time, and parallelism
    /// - Best choice for new systems
    /// 
    /// Usage in login:
    /// <code>
    /// var storedHash = authenticatable.GetPasswordHash();
    /// var isValid = passwordHasher.VerifyPassword(providedPassword, storedHash);
    /// </code>
    /// </summary>
    string GetPasswordHash();

    /// <summary>
    /// Whether account is active
    /// 
    /// Inactive accounts:
    /// - Cannot login (even with correct password)
    /// - Existing sessions remain valid until token expiry
    /// - Can be reactivated by admin
    /// 
    /// Used for:
    /// - Temporary account suspension
    /// - Employee termination (soft delete)
    /// - Security lockout (too many failed login attempts)
    /// - Administrative disable
    /// 
    /// Login flow:
    /// <code>
    /// if (!authenticatable.GetIsActive())
    /// {
    ///     return Error("Account is disabled. Contact administrator.");
    /// }
    /// </code>
    /// 
    /// Session handling:
    /// - Active sessions not automatically terminated when account disabled
    /// - Admin should manually revoke all sessions for disabled accounts
    /// - RefreshToken validation should also check IsActive
    /// </summary>
    bool GetIsActive();

    /// <summary>
    /// Type of authenticated user (Operator or User)
    /// 
    /// Determines:
    /// - Data access scope:
    ///   * Operator: Global access to all data
    ///   * User: Scoped access based on ScopeLevel
    /// 
    /// - Available features:
    ///   * Operator: System management, company creation, global settings
    ///   * User: Employee features, scoped management
    /// 
    /// - UI permissions:
    ///   * Operator: Admin panel, system configuration
    ///   * User: Employee portal, scoped dashboard
    /// 
    /// - Authorization policies:
    ///   * RequireOperator: Only Operators allowed
    ///   * RequireUser: Only Users allowed
    ///   * RequireAny: Both allowed
    /// 
    /// JWT Token:
    /// - Claim name: "UserType"
    /// - Claim value: "Operator" or "User"
    /// - Used in [Authorize] policies
    /// 
    /// Example authorization:
    /// <code>
    /// [Authorize(Policy = "RequireOperator")]
    /// public async Task<IResult> CreateCompany(...)
    /// {
    ///     // Only Operators can create companies
    /// }
    /// 
    /// [Authorize(Policy = "RequireUser")]
    /// public async Task<IResult> ViewMyProfile(...)
    /// {
    ///     // Only Users (employees) can view profile
    /// }
    /// </code>
    /// </summary>
    UserType GetUserType();
}
