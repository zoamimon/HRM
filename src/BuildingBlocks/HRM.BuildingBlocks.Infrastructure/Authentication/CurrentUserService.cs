using System.Security.Claims;
using HRM.BuildingBlocks.Application.Abstractions.Authentication;
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
    /// Gets the current user's type (Operator or User) from JWT "UserType" claim
    /// </summary>
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

    /// <summary>
    /// Gets the current user's scope level from JWT "ScopeLevel" claim
    /// Only applicable for Users, null for Operators
    /// </summary>
    public ScopeLevel? ScopeLevel
    {
        get
        {
            // Operators don't have scope level (global access)
            if (UserType == Domain.Enums.UserType.Operator)
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
    /// Gets the current user's employee ID from JWT "EmployeeId" claim
    /// Only applicable for Users, null for Operators
    /// </summary>
    public Guid? EmployeeId
    {
        get
        {
            // Operators don't have employee ID
            if (UserType == Domain.Enums.UserType.Operator)
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
    /// Checks if the current user is an Operator
    /// Operators have global access without data scoping
    /// </summary>
    /// <returns>True if user is an Operator, false otherwise</returns>
    public bool IsOperator() => UserType == Domain.Enums.UserType.Operator;

    /// <summary>
    /// Checks if the current user is a User (employee)
    /// Users have scoped access based on ScopeLevel
    /// </summary>
    /// <returns>True if user is a User (employee), false otherwise</returns>
    public bool IsUser() => UserType == Domain.Enums.UserType.User;
}
