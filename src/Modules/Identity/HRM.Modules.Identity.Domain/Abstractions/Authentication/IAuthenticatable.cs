using HRM.Modules.Identity.Domain.Enums;

namespace HRM.Modules.Identity.Domain.Abstractions.Authentication;

/// <summary>
/// Interface for authenticatable entities (Operator, Account).
/// Provides common contract for authentication and authorization.
///
/// This interface belongs to the Identity module (authentication BC).
///
/// Implementing classes:
/// - Operator: System administrator entity
/// - Account: Unified authentication entity (System + Employee)
/// </summary>
public interface IAuthenticatable
{
    /// <summary>
    /// Entity unique identifier.
    /// Used as JWT 'sub' claim.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Username for login.
    /// Must be unique across all authenticatable entities.
    /// </summary>
    string GetUsername();

    /// <summary>
    /// Email address.
    /// Must be unique across all authenticatable entities.
    /// </summary>
    string GetEmail();

    /// <summary>
    /// Hashed password (BCrypt or Argon2).
    /// NEVER store or log plain text passwords.
    /// </summary>
    string GetPasswordHash();

    /// <summary>
    /// Whether account is active and can login.
    /// </summary>
    bool GetIsActive();

    /// <summary>
    /// Type of authenticated user (deprecated - use GetAccountType).
    /// </summary>
    [Obsolete("Use GetAccountType() instead.")]
    UserType GetUserType();

    /// <summary>
    /// Type of authenticated account (System or Employee).
    /// This is the canonical method.
    /// </summary>
    AccountType GetAccountType()
    {
#pragma warning disable CS0618 // Suppress obsolete warning for transition
        return GetUserType().ToAccountType();
#pragma warning restore CS0618
    }
}
