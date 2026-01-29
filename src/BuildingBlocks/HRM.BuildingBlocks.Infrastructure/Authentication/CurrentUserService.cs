using System.Security.Claims;
using HRM.BuildingBlocks.Application.Abstractions.Authentication;
using HRM.BuildingBlocks.Domain.Entities;
using HRM.BuildingBlocks.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace HRM.BuildingBlocks.Infrastructure.Authentication;

/// <summary>
/// Implementation of ICurrentUserService that reads user information from JWT claims
/// Extracts authenticated user context from HttpContext.User
///
/// JWT Claim Mapping:
/// - "sub" → UserId (NameIdentifier)
/// - "name" → Username (Name)
/// - "email" → Email (Email)
/// - "UserType" → UserType enum (custom claim)
/// - "ScopeLevel" → ScopeLevel enum (custom claim, only for Users)
/// - "EmployeeId" → EmployeeId (custom claim, only for Users)
/// - ClaimTypes.Role → Roles array (normalized by RolesClaimsTransformation middleware)
///
/// Role Handling:
/// - Roles are normalized by RolesClaimsTransformation middleware
/// - Multiple role formats are supported (comma-separated, multiple claims, etc.)
/// - All roles are transformed into standard ClaimTypes.Role claims
/// - Native ASP.NET Core authorization works: [Authorize(Roles = "Admin")], User.IsInRole("Admin")
///
/// Lifecycle:
/// - Registered as Scoped service (per HTTP request)
/// - Each request gets its own instance
/// - HttpContext is scoped per request (thread-safe)
///
/// Authentication Flow:
/// 1. Client sends JWT token in Authorization header
/// 2. JWT authentication middleware validates token
/// 3. Middleware populates HttpContext.User.Claims
/// 4. RolesClaimsTransformation normalizes role claims
/// 5. CurrentUserService reads claims from HttpContext.User
/// 6. Application code uses ICurrentUserService for authorization
///
/// Usage Example:
/// <code>
/// public class CreateDepartmentCommandHandler
/// {
///     private readonly ICurrentUserService _currentUser;
///
///     public async Task<Result<Guid>> Handle(...)
///     {
///         if (!_currentUser.IsAuthenticated)
///             return Result.Failure(new UnauthorizedError(...));
///
///         if (_currentUser.IsOperator())
///         {
///             // Operators have global access
///         }
///         else if (_currentUser.ScopeLevel == ScopeLevel.Company)
///         {
///             // Company-level users can create departments
///         }
///         else
///         {
///             return Result.Failure(new ForbiddenError(...));
///         }
///     }
/// }
/// </code>
///
/// Thread Safety:
/// - HttpContext is scoped per request (thread-safe)
/// - Claims are immutable once set
/// - No shared state between requests
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Constructor with HttpContext accessor
    /// </summary>
    /// <param name="httpContextAccessor">Accessor for current HTTP context</param>
    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <summary>
    /// Get current HttpContext.User (ClaimsPrincipal)
    /// </summary>
    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    /// <summary>
    /// Gets the current user's unique identifier from JWT "sub" claim
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when user is not authenticated</exception>
    public Guid UserId
    {
        get
        {
            if (!IsAuthenticated)
            {
                throw new InvalidOperationException(
                    "Cannot access UserId when user is not authenticated. " +
                    "Check IsAuthenticated property first."
                );
            }

            var userIdClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User?.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                throw new InvalidOperationException(
                    "User ID claim is missing or invalid in JWT token."
                );
            }

            return userId;
        }
    }

    /// <summary>
    /// Gets the current user's username from JWT "name" claim
    /// </summary>
    public string? Username => User?.FindFirst(ClaimTypes.Name)?.Value
                            ?? User?.FindFirst("name")?.Value;

    /// <summary>
    /// Gets the current user's email from JWT "email" claim
    /// </summary>
    public string? Email => User?.FindFirst(ClaimTypes.Email)?.Value
                         ?? User?.FindFirst("email")?.Value;

    /// <summary>
    /// Gets the current user's account type (System or Employee).
    /// This is the canonical property - use this instead of UserType.
    /// </summary>
    public AccountType AccountType
    {
        get
        {
            var userTypeClaim = User?.FindFirst("UserType")?.Value;

            if (string.IsNullOrEmpty(userTypeClaim))
            {
                // Default to Employee if not specified (most restrictive)
                return AccountType.Employee;
            }

            // Try parsing as AccountType first
            if (Enum.TryParse<AccountType>(userTypeClaim, ignoreCase: true, out var accountType))
            {
                return accountType;
            }

            // Fallback: try parsing as UserType and convert
#pragma warning disable CS0618 // UserType is obsolete
            if (Enum.TryParse<UserType>(userTypeClaim, ignoreCase: true, out var userType))
            {
                return userType.ToAccountType();
            }
#pragma warning restore CS0618

            // Default to Employee if parse fails (most restrictive)
            return AccountType.Employee;
        }
    }

    /// <summary>
    /// Gets the current user's type (deprecated - use AccountType).
    /// </summary>
#pragma warning disable CS0618 // UserType is obsolete
    public UserType UserType
    {
        get
        {
            var userTypeClaim = User?.FindFirst("UserType")?.Value;

            if (string.IsNullOrEmpty(userTypeClaim))
            {
                // Default to User if not specified (most restrictive)
                return Domain.Enums.UserType.User;
            }

            if (Enum.TryParse<UserType>(userTypeClaim, ignoreCase: true, out var userType))
            {
                return userType;
            }

            // Default to User if parse fails (most restrictive)
            return Domain.Enums.UserType.User;
        }
    }
#pragma warning restore CS0618

    /// <summary>
    /// Gets the current user's scope level from JWT "ScopeLevel" claim.
    /// Only applicable for Employee accounts, null for System accounts.
    /// </summary>
    public ScopeLevel? ScopeLevel
    {
        get
        {
            // System accounts don't have scope level (global access)
            if (AccountType == AccountType.System)
            {
                return null;
            }

            var scopeLevelClaim = User?.FindFirst("ScopeLevel")?.Value;

            if (string.IsNullOrEmpty(scopeLevelClaim))
            {
                // Default to Employee level if not specified (most restrictive)
                return Domain.Enums.ScopeLevel.Employee;
            }

            if (Enum.TryParse<ScopeLevel>(scopeLevelClaim, ignoreCase: true, out var scopeLevel))
            {
                return scopeLevel;
            }

            // Default to Employee level if parse fails (most restrictive)
            return Domain.Enums.ScopeLevel.Employee;
        }
    }

    /// <summary>
    /// Gets the current user's employee ID from JWT "EmployeeId" claim.
    /// Only applicable for Employee accounts, null for System accounts.
    /// </summary>
    public Guid? EmployeeId
    {
        get
        {
            // System accounts don't have employee ID
            if (AccountType == AccountType.System)
            {
                return null;
            }

            var employeeIdClaim = User?.FindFirst("EmployeeId")?.Value;

            if (string.IsNullOrEmpty(employeeIdClaim))
            {
                return null;
            }

            if (Guid.TryParse(employeeIdClaim, out var employeeId))
            {
                return employeeId;
            }

            return null;
        }
    }

    /// <summary>
    /// Gets the current user's roles from normalized ClaimTypes.Role claims
    /// Roles are normalized by RolesClaimsTransformation middleware from various formats
    /// Returns read-only collection of role names
    /// Never returns null - returns empty collection if no roles
    /// </summary>
    public IReadOnlyCollection<string> Roles
    {
        get
        {
            if (User == null)
            {
                return Array.Empty<string>();
            }

            // Read all normalized ClaimTypes.Role claims
            // RolesClaimsTransformation already parsed and normalized all role formats
            return User
                .FindAll(ClaimTypes.Role)
                .Select(c => c.Value)
                .ToArray();
        }
    }

    /// <summary>
    /// Indicates whether the current request is authenticated
    /// Checks if HttpContext.User.Identity.IsAuthenticated is true
    /// </summary>
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    /// <summary>
    /// Checks if the current user has a specific role
    /// Uses native ASP.NET Core User.IsInRole() with normalized ClaimTypes.Role claims
    /// Case-sensitive comparison (depends on role normalization)
    /// </summary>
    /// <param name="role">Role name to check</param>
    /// <returns>True if user has the role, false otherwise</returns>
    public bool HasRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        // Use native ASP.NET Core IsInRole() with normalized claims
        return User?.IsInRole(role) ?? false;
    }

    /// <summary>
    /// Checks if the current user has a specific role
    /// Alias for HasRole - provides cleaner syntax for role checks in Application layer
    /// Uses native ASP.NET Core User.IsInRole() with normalized ClaimTypes.Role claims
    /// </summary>
    /// <param name="role">Role name to check</param>
    /// <returns>True if user has the role, false otherwise</returns>
    public bool IsInRole(string role) => HasRole(role);

    /// <summary>
    /// Checks if the current user is a System account (operator/admin).
    /// System accounts have global access without data scoping.
    /// </summary>
    /// <returns>True if user is a System account, false otherwise</returns>
    public bool IsSystemAccount() => AccountType == AccountType.System;

    /// <summary>
    /// Checks if the current user is an Employee account.
    /// Employee accounts have scoped access based on ScopeLevel.
    /// </summary>
    /// <returns>True if user is an Employee account, false otherwise</returns>
    public bool IsEmployeeAccount() => AccountType == AccountType.Employee;

    /// <summary>
    /// Checks if the current user is an Operator (deprecated - use IsSystemAccount).
    /// </summary>
    [Obsolete("Use IsSystemAccount() instead")]
    public bool IsOperator() => AccountType == AccountType.System;

    /// <summary>
    /// Checks if the current user is a User (deprecated - use IsEmployeeAccount).
    /// </summary>
    [Obsolete("Use IsEmployeeAccount() instead")]
    public bool IsUser() => AccountType == AccountType.Employee;
}
