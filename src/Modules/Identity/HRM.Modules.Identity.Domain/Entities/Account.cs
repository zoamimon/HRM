using HRM.BuildingBlocks.Domain.Abstractions.Audit;
using HRM.BuildingBlocks.Domain.Entities;
using HRM.BuildingBlocks.Domain.Enums;

namespace HRM.Modules.Identity.Domain.Entities;

/// <summary>
/// Unified Account entity for authentication.
/// Supports both System accounts and Employee accounts.
///
/// Architecture:
/// - Account: Authentication data (login, password, sessions)
/// - SystemProfile: Additional data for System accounts (roles, permissions)
/// - EmployeeProfile: Link to Employee entity for Employee accounts
///
/// Security Audit:
/// This entity implements ISecurityAuditable for compliance-level tracking:
/// - PasswordChangedAtUtc: When password was last changed
/// - LastFailedLoginAtUtc: When last failed login occurred
/// - TwoFactorChangedAtUtc: When 2FA was enabled/disabled
/// - StatusChangedAtUtc: When account status was changed
///
/// This replaces the separate Operator entity for a unified auth experience.
/// </summary>
public class Account : AuditableEntity, ISecurityAuditable
{
    /// <summary>
    /// Unique username for login
    /// </summary>
    public string Username { get; private set; } = string.Empty;

    /// <summary>
    /// Email address (also unique, used for login)
    /// </summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>
    /// Hashed password
    /// </summary>
    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>
    /// Account type (System or Employee)
    /// </summary>
    public AccountType AccountType { get; private set; }

    /// <summary>
    /// Current account status
    /// </summary>
    public AccountStatus Status { get; private set; } = AccountStatus.Pending;

    /// <summary>
    /// Full name for display
    /// </summary>
    public string FullName { get; private set; } = string.Empty;

    /// <summary>
    /// Phone number (optional)
    /// </summary>
    public string? PhoneNumber { get; private set; }

    /// <summary>
    /// Whether two-factor authentication is enabled
    /// </summary>
    public bool IsTwoFactorEnabled { get; private set; }

    /// <summary>
    /// Secret key for TOTP-based 2FA
    /// </summary>
    public string? TwoFactorSecretKey { get; private set; }

    /// <summary>
    /// When the account was activated
    /// </summary>
    public DateTime? ActivatedAtUtc { get; private set; }

    /// <summary>
    /// Last successful login timestamp
    /// </summary>
    public DateTime? LastLoginAtUtc { get; private set; }

    /// <summary>
    /// Number of consecutive failed login attempts
    /// </summary>
    public int FailedLoginAttempts { get; private set; }

    /// <summary>
    /// Account lockout expiry (null if not locked)
    /// </summary>
    public DateTime? LockedUntilUtc { get; private set; }

    #region ISecurityAuditable

    /// <summary>
    /// When the password was last changed (UTC).
    /// NULL if password never changed since account creation.
    /// </summary>
    public DateTime? PasswordChangedAtUtc { get; private set; }

    /// <summary>
    /// When the last failed login attempt occurred (UTC).
    /// Used for brute force detection and security monitoring.
    /// </summary>
    public DateTime? LastFailedLoginAtUtc { get; private set; }

    /// <summary>
    /// When two-factor authentication was last enabled or disabled (UTC).
    /// Tracks security posture changes.
    /// </summary>
    public DateTime? TwoFactorChangedAtUtc { get; private set; }

    /// <summary>
    /// When the account status was last changed (UTC).
    /// Tracks activation, suspension, deactivation events.
    /// </summary>
    public DateTime? StatusChangedAtUtc { get; private set; }

    #endregion

    // Audit fields inherited from AuditableEntity:
    // - CreatedAtUtc, ModifiedAtUtc, CreatedById, ModifiedById

    // Navigation properties (configured in EF)
    // public SystemProfile? SystemProfile { get; private set; }
    // public EmployeeProfile? EmployeeProfile { get; private set; }

    // Private constructor for EF
    private Account() { }

    /// <summary>
    /// Create a new System account
    /// </summary>
    public static Account CreateSystemAccount(
        string username,
        string email,
        string passwordHash,
        string fullName,
        string? phoneNumber = null)
    {
        return new Account
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            PasswordHash = passwordHash,
            FullName = fullName,
            PhoneNumber = phoneNumber,
            AccountType = AccountType.System,
            Status = AccountStatus.Pending
            // CreatedAtUtc is set automatically by AuditableEntity constructor
        };
    }

    /// <summary>
    /// Create a new Employee account
    /// </summary>
    public static Account CreateEmployeeAccount(
        string username,
        string email,
        string passwordHash,
        string fullName,
        string? phoneNumber = null)
    {
        return new Account
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            PasswordHash = passwordHash,
            FullName = fullName,
            PhoneNumber = phoneNumber,
            AccountType = AccountType.Employee,
            Status = AccountStatus.Pending
            // CreatedAtUtc is set automatically by AuditableEntity constructor
        };
    }

    /// <summary>
    /// Activate the account
    /// </summary>
    public void Activate()
    {
        if (Status == AccountStatus.Active)
            return;

        Status = AccountStatus.Active;
        ActivatedAtUtc = DateTime.UtcNow;
        StatusChangedAtUtc = DateTime.UtcNow;
        MarkAsModified();
    }

    /// <summary>
    /// Suspend the account
    /// </summary>
    public void Suspend()
    {
        if (Status == AccountStatus.Suspended)
            return;

        Status = AccountStatus.Suspended;
        StatusChangedAtUtc = DateTime.UtcNow;
        MarkAsModified();
    }

    /// <summary>
    /// Deactivate the account
    /// </summary>
    public void Deactivate()
    {
        if (Status == AccountStatus.Deactivated)
            return;

        Status = AccountStatus.Deactivated;
        StatusChangedAtUtc = DateTime.UtcNow;
        MarkAsModified();
    }

    /// <summary>
    /// Record successful login
    /// </summary>
    public void RecordLogin()
    {
        LastLoginAtUtc = DateTime.UtcNow;
        FailedLoginAttempts = 0;
        LockedUntilUtc = null;
        MarkAsModified();
    }

    /// <summary>
    /// Record failed login attempt
    /// </summary>
    public void RecordFailedLogin(int maxAttempts = 5, int lockoutMinutes = 30)
    {
        FailedLoginAttempts++;
        LastFailedLoginAtUtc = DateTime.UtcNow;

        if (FailedLoginAttempts >= maxAttempts)
        {
            LockedUntilUtc = DateTime.UtcNow.AddMinutes(lockoutMinutes);
        }

        MarkAsModified();
    }

    /// <summary>
    /// Check if account is currently locked (QUERY - no side effects)
    /// </summary>
    public bool IsLocked() =>
        LockedUntilUtc.HasValue && LockedUntilUtc.Value > DateTime.UtcNow;

    /// <summary>
    /// Attempt to unlock account if lockout has expired (COMMAND - modifies state)
    /// Call this before login attempts to auto-clear expired lockouts
    /// </summary>
    /// <returns>True if account was unlocked, false if still locked or wasn't locked</returns>
    public bool TryUnlock()
    {
        if (!LockedUntilUtc.HasValue)
            return false;

        if (LockedUntilUtc.Value <= DateTime.UtcNow)
        {
            LockedUntilUtc = null;
            FailedLoginAttempts = 0;
            MarkAsModified();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Check if account can login
    /// </summary>
    public bool CanLogin()
    {
        return Status == AccountStatus.Active && !IsLocked();
    }

    /// <summary>
    /// Update password
    /// </summary>
    public void UpdatePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        PasswordChangedAtUtc = DateTime.UtcNow;
        MarkAsModified();
    }

    /// <summary>
    /// Update profile information
    /// </summary>
    public void UpdateProfile(string fullName, string? phoneNumber)
    {
        FullName = fullName;
        PhoneNumber = phoneNumber;
        MarkAsModified();
    }

    /// <summary>
    /// Enable two-factor authentication
    /// </summary>
    public void EnableTwoFactor(string secretKey)
    {
        IsTwoFactorEnabled = true;
        TwoFactorSecretKey = secretKey;
        TwoFactorChangedAtUtc = DateTime.UtcNow;
        MarkAsModified();
    }

    /// <summary>
    /// Disable two-factor authentication
    /// </summary>
    public void DisableTwoFactor()
    {
        IsTwoFactorEnabled = false;
        TwoFactorSecretKey = null;
        TwoFactorChangedAtUtc = DateTime.UtcNow;
        MarkAsModified();
    }

    /// <summary>
    /// Check if this is a system account
    /// </summary>
    public bool IsSystemAccount => AccountType == AccountType.System;

    /// <summary>
    /// Check if this is an employee account
    /// </summary>
    public bool IsEmployeeAccount => AccountType == AccountType.Employee;
}
