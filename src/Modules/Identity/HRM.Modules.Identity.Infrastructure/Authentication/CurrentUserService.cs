using System.Security.Claims;
using HRM.Modules.Identity.Application.Abstractions.Authentication;
using HRM.Modules.Identity.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace HRM.Modules.Identity.Infrastructure.Authentication;

/// <summary>
/// Implementation of ICurrentUserService that reads user information from JWT claims.
/// Lives in Identity.Infrastructure — only Identity module has typed access to AccountType/ScopeLevel.
///
/// Other modules use IExecutionContext (BuildingBlocks) which provides only primitives.
/// DI registers this as both ICurrentUserService and IExecutionContext.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    /// <inheritdoc />
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

    /// <inheritdoc />
    public string? Username => User?.FindFirst(ClaimTypes.Name)?.Value
                            ?? User?.FindFirst("name")?.Value;

    /// <inheritdoc />
    public string? Email => User?.FindFirst(ClaimTypes.Email)?.Value
                         ?? User?.FindFirst("email")?.Value;

    /// <inheritdoc />
    public AccountType AccountType
    {
        get
        {
            var userTypeClaim = User?.FindFirst("UserType")?.Value;

            if (string.IsNullOrEmpty(userTypeClaim))
            {
                return AccountType.Employee;
            }

            if (Enum.TryParse<AccountType>(userTypeClaim, ignoreCase: true, out var accountType))
            {
                return accountType;
            }

#pragma warning disable CS0618
            if (Enum.TryParse<UserType>(userTypeClaim, ignoreCase: true, out var userType))
            {
                return userType.ToAccountType();
            }
#pragma warning restore CS0618

            return AccountType.Employee;
        }
    }

#pragma warning disable CS0618
    /// <inheritdoc />
    public UserType UserType
    {
        get
        {
            var userTypeClaim = User?.FindFirst("UserType")?.Value;

            if (string.IsNullOrEmpty(userTypeClaim))
            {
                return Domain.Enums.UserType.User;
            }

            if (Enum.TryParse<UserType>(userTypeClaim, ignoreCase: true, out var userType))
            {
                return userType;
            }

            return Domain.Enums.UserType.User;
        }
    }
#pragma warning restore CS0618

    /// <inheritdoc />
    public ScopeLevel? ScopeLevel
    {
        get
        {
            if (AccountType == AccountType.System)
            {
                return null;
            }

            var scopeLevelClaim = User?.FindFirst("ScopeLevel")?.Value;

            if (string.IsNullOrEmpty(scopeLevelClaim))
            {
                return Domain.Enums.ScopeLevel.Employee;
            }

            if (Enum.TryParse<ScopeLevel>(scopeLevelClaim, ignoreCase: true, out var scopeLevel))
            {
                return scopeLevel;
            }

            return Domain.Enums.ScopeLevel.Employee;
        }
    }

    /// <inheritdoc />
    public Guid? EmployeeId
    {
        get
        {
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

    /// <inheritdoc />
    public IReadOnlyCollection<string> Roles
    {
        get
        {
            if (User == null)
            {
                return Array.Empty<string>();
            }

            return User
                .FindAll(ClaimTypes.Role)
                .Select(c => c.Value)
                .ToArray();
        }
    }

    /// <inheritdoc />
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    /// <inheritdoc />
    public bool HasRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        return User?.IsInRole(role) ?? false;
    }

    /// <inheritdoc />
    public bool IsInRole(string role) => HasRole(role);

    /// <inheritdoc />
    public string? GetClaimValue(string claimType)
    {
        if (string.IsNullOrWhiteSpace(claimType))
        {
            return null;
        }

        return User?.FindFirst(claimType)?.Value;
    }

    /// <inheritdoc />
    public bool IsSystemAccount() => AccountType == AccountType.System;

    /// <inheritdoc />
    public bool IsEmployeeAccount() => AccountType == AccountType.Employee;

    /// <inheritdoc />
    [Obsolete("Use IsSystemAccount() instead")]
    public bool IsOperator() => AccountType == AccountType.System;

    /// <inheritdoc />
    [Obsolete("Use IsEmployeeAccount() instead")]
    public bool IsUser() => AccountType == AccountType.Employee;
}
