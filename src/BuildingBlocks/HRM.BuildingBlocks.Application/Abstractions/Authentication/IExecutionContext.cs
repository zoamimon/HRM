namespace HRM.BuildingBlocks.Application.Abstractions.Authentication;

/// <summary>
/// Minimal execution context for the current authenticated user.
/// Contains ONLY primitive/technical data — no business vocabulary.
///
/// Design Principles (kgrzybek-style Module Isolation):
/// - This interface lives in BuildingBlocks (shared infrastructure)
/// - Contains only primitive types: Guid, string, bool
/// - NO business enums (AccountType, ScopeLevel) — those belong to Identity module
/// - Other modules use this interface for basic user context (UserId, Roles)
/// - Identity module extends this with ICurrentUserService for typed access
///
/// Usage in non-Identity modules:
/// <code>
/// public class CreateDepartmentCommandHandler
/// {
///     private readonly IExecutionContext _context;
///
///     public async Task&lt;Result&gt; Handle(...)
///     {
///         if (!_context.IsAuthenticated)
///             return Result.Failure(new UnauthorizedError(...));
///
///         var userId = _context.UserId;
///         // Use userId for audit, no need to know AccountType
///     }
/// }
/// </code>
///
/// Identity module provides typed extension via ICurrentUserService:
/// <code>
/// // Only in Identity module:
/// public interface ICurrentUserService : IExecutionContext
/// {
///     AccountType AccountType { get; }
///     ScopeLevel? ScopeLevel { get; }
/// }
/// </code>
/// </summary>
public interface IExecutionContext
{
    /// <summary>
    /// Current user's unique identifier (from JWT 'sub' claim).
    /// Throws InvalidOperationException if not authenticated.
    /// </summary>
    Guid UserId { get; }

    /// <summary>
    /// Current user's username (from JWT 'name' claim).
    /// </summary>
    string? Username { get; }

    /// <summary>
    /// Current user's email (from JWT 'email' claim).
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// Whether the current request is authenticated.
    /// Always check this before accessing UserId.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Current user's roles (from JWT role claims).
    /// Never null — returns empty collection if no roles.
    /// </summary>
    IReadOnlyCollection<string> Roles { get; }

    /// <summary>
    /// Check if current user has a specific role.
    /// </summary>
    bool HasRole(string role);

    /// <summary>
    /// Check if current user has a specific role (alias for HasRole).
    /// </summary>
    bool IsInRole(string role);

    /// <summary>
    /// Get a raw claim value by claim type.
    /// Returns null if claim not found.
    /// This allows modules to read custom claims without typed dependencies.
    /// </summary>
    string? GetClaimValue(string claimType);
}
