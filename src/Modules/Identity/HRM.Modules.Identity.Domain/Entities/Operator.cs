using HRM.Modules.Identity.Domain.Abstractions.Authentication;
using HRM.BuildingBlocks.Domain.Entities;
using HRM.Modules.Identity.Domain.Enums;
using HRM.Modules.Identity.Domain.Events;

namespace HRM.Modules.Identity.Domain.Entities;

/// <summary>
/// Operator aggregate root
/// Represents internal system user with administrative privileges
///
/// Operator vs User:
/// - Operator: Internal staff/admin with system access
/// - User: External end-users (managed in different module)
///
/// Responsibilities:
/// - Identity management (username, email, password)
/// - Access control (status, 2FA)
/// - Security (failed login tracking, account lockout)
/// - Audit trail (via Entity base class)
///
/// Business Rules:
/// - Username must be unique
/// - Email must be unique
/// - Password must meet complexity requirements (enforced in application layer)
/// - Only Active operators can login
/// - Account locked after 5 failed login attempts
/// - Lock duration: 30 minutes
///
/// Domain Events:
/// - OperatorRegisteredDomainEvent: Raised when new operator registered
/// - OperatorActivatedDomainEvent: Raised when operator activated
/// </summary>
public sealed class Operator : SoftDeletableEntity, IAggregateRoot, IAuthenticatable
{
    /// <summary>
    /// Unique username for login
    /// </summary>
    public string Username { get; private set; } = default!;

    /// <summary>
    /// Email address (unique identifier, used for notifications)
    /// </summary>
    public string Email { get; private set; } = default!;

    /// <summary>
    /// BCrypt hashed password
    /// Never store plain text passwords
    /// </summary>
    public string PasswordHash { get; private set; } = default!;

    /// <summary>
    /// Operator's full name
    /// </summary>
    public string FullName { get; private set; } = default!;

    /// <summary>
    /// Optional phone number
    /// Can be used for 2FA SMS
    /// </summary>
    public string? PhoneNumber { get; private set; }

    /// <summary>
    /// Current account status
    /// Controls login access
    /// </summary>
    public OperatorStatus Status { get; private set; }

    /// <summary>
    /// When the operator was activated (moved to Active status)
    /// NULL if never activated (still Pending)
    /// </summary>
    public DateTime? ActivatedAtUtc { get; private set; }

    /// <summary>
    /// Last successful login timestamp
    /// Used for session tracking and security audits
    /// </summary>
    public DateTime? LastLoginAtUtc { get; private set; }

    /// <summary>
    /// Whether 2FA (Two-Factor Authentication) is enabled
    /// Future: Can be enforced for all operators
    /// </summary>
    public bool IsTwoFactorEnabled { get; private set; }

    /// <summary>
    /// 2FA secret key (TOTP)
    /// Encrypted storage recommended in production
    /// </summary>
    public string? TwoFactorSecret { get; private set; }

    /// <summary>
    /// Number of consecutive failed login attempts
    /// Reset to 0 on successful login
    /// Used for account lockout logic
    /// </summary>
    public int FailedLoginAttempts { get; private set; }

    /// <summary>
    /// Account locked until this time (UTC)
    /// NULL if not locked
    /// Locked after 5 failed login attempts for 30 minutes
    /// </summary>
    public DateTime? LockedUntilUtc { get; private set; }

    /// <summary>
    /// Private parameterless constructor for EF Core
    /// </summary>
    private Operator()
    {
    }

    /// <summary>
    /// Factory method to register a new operator
    /// Creates operator in Pending status, requires manual activation
    ///
    /// Business Rules:
    /// - Username must be unique (validated in application layer)
    /// - Email must be unique (validated in application layer)
    /// - Password must be hashed with BCrypt (done in application layer)
    /// - Initial status is Pending
    /// - 2FA disabled by default
    ///
    /// Domain Event:
    /// - Raises OperatorRegisteredDomainEvent
    /// </summary>
    /// <param name="username">Unique username (3-50 chars)</param>
    /// <param name="email">Unique email address</param>
    /// <param name="passwordHash">BCrypt hashed password</param>
    /// <param name="fullName">Operator's full name</param>
    /// <param name="phoneNumber">Optional phone number</param>
    /// <returns>New operator in Pending status</returns>
    public static Operator Register(
        string username,
        string email,
        string passwordHash,
        string fullName,
        string? phoneNumber = null)
    {
        var @operator = new Operator
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            PasswordHash = passwordHash,
            FullName = fullName,
            PhoneNumber = phoneNumber,
            Status = OperatorStatus.Pending,
            IsTwoFactorEnabled = false,
            FailedLoginAttempts = 0
        };

        // Raise domain event for integration with other modules
        @operator.AddDomainEvent(new OperatorRegisteredDomainEvent(
            @operator.Id,
            @operator.Username,
            @operator.Email,
            @operator.FullName
        ));

        return @operator;
    }

    /// <summary>
    /// Activate the operator account
    /// Moves operator from Pending to Active status
    /// Only Active operators can login
    ///
    /// Business Rules:
    /// - Cannot activate already Active operator
    /// - Can reactivate Suspended operator
    /// - Sets ActivatedAtUtc timestamp
    ///
    /// Domain Event:
    /// - Raises OperatorActivatedDomainEvent
    /// </summary>
    /// <exception cref="InvalidOperationException">If operator is already active</exception>
    public void Activate()
    {
        if (Status == OperatorStatus.Active)
        {
            throw new InvalidOperationException(
                $"Operator '{Username}' is already active. Activation skipped."
            );
        }

        Status = OperatorStatus.Active;
        ActivatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new OperatorActivatedDomainEvent(Id, Username));
    }

    /// <summary>
    /// Suspend the operator account
    /// Temporarily disables login access
    /// Can be reactivated later
    ///
    /// Use Cases:
    /// - Policy violation
    /// - Security investigation
    /// - Temporary leave of absence
    /// </summary>
    /// <exception cref="InvalidOperationException">If operator is not active</exception>
    public void Suspend()
    {
        if (Status != OperatorStatus.Active)
        {
            throw new InvalidOperationException(
                $"Only Active operators can be suspended. Current status: {Status}"
            );
        }

        Status = OperatorStatus.Suspended;
    }

    /// <summary>
    /// Deactivate the operator account permanently
    /// Typically irreversible - use soft delete instead for audit
    ///
    /// Recommendation: Use Delete() from Entity base class instead
    /// for better audit trail
    /// </summary>
    public void Deactivate()
    {
        Status = OperatorStatus.Deactivated;
    }

    /// <summary>
    /// Record successful login
    /// Resets failed login attempts and unlock account
    ///
    /// Called by authentication service after successful login
    /// </summary>
    public void RecordLogin()
    {
        LastLoginAtUtc = DateTime.UtcNow;
        FailedLoginAttempts = 0;
        LockedUntilUtc = null;
    }

    /// <summary>
    /// Record failed login attempt
    /// Increments counter and locks account after 5 failures
    ///
    /// Security:
    /// - Lock account for 30 minutes after 5 failed attempts
    /// - Prevents brute force attacks
    /// - Requires manual unlock or time expiry
    /// </summary>
    public void RecordFailedLogin()
    {
        FailedLoginAttempts++;

        // Lock account after 5 failed attempts
        if (FailedLoginAttempts >= 5)
        {
            LockedUntilUtc = DateTime.UtcNow.AddMinutes(30);
        }
    }

    /// <summary>
    /// Check if account is currently locked
    /// Account locked if LockedUntilUtc is in the future
    /// </summary>
    /// <returns>True if account is locked, false otherwise</returns>
    public bool IsLocked()
    {
        return LockedUntilUtc.HasValue && LockedUntilUtc.Value > DateTime.UtcNow;
    }

    /// <summary>
    /// Manually unlock the account
    /// Resets failed login attempts
    /// Called by admin to unlock account before timeout
    /// </summary>
    public void Unlock()
    {
        FailedLoginAttempts = 0;
        LockedUntilUtc = null;
    }

    /// <summary>
    /// Enable Two-Factor Authentication
    /// Requires 2FA secret to be set
    /// Future: Can enforce 2FA for all operators
    /// </summary>
    /// <param name="twoFactorSecret">TOTP secret key</param>
    public void EnableTwoFactor(string twoFactorSecret)
    {
        TwoFactorSecret = twoFactorSecret;
        IsTwoFactorEnabled = true;
    }

    /// <summary>
    /// Disable Two-Factor Authentication
    /// Clears 2FA secret
    /// </summary>
    public void DisableTwoFactor()
    {
        TwoFactorSecret = null;
        IsTwoFactorEnabled = false;
    }

    /// <summary>
    /// Update operator password
    /// Password must be hashed before calling this method
    /// </summary>
    /// <param name="newPasswordHash">BCrypt hashed new password</param>
    public void ChangePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
    }

    /// <summary>
    /// Update operator profile
    /// Allows updating non-security fields
    /// </summary>
    /// <param name="fullName">New full name</param>
    /// <param name="phoneNumber">New phone number (optional)</param>
    public void UpdateProfile(string fullName, string? phoneNumber)
    {
        FullName = fullName;
        PhoneNumber = phoneNumber;
    }

    // IAuthenticatable implementation
    public string GetUsername() => Username;
    public string GetEmail() => Email;
    public string GetPasswordHash() => PasswordHash;
    public bool GetIsActive() => Status == OperatorStatus.Active;

#pragma warning disable CS0618 // Implementing obsolete interface method
    public UserType GetUserType() => UserType.Operator;
#pragma warning restore CS0618

    public AccountType GetAccountType() => AccountType.System;
}
