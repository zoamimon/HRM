using HRM.BuildingBlocks.Domain.Abstractions;
using HRM.BuildingBlocks.Domain.Enums;

namespace HRM.BuildingBlocks.Domain.Entities;

/// <summary>
/// Account type enumeration
/// </summary>
public enum AccountType
{
    /// <summary>
    /// System account (internal operators, admins)
    /// Has access to system configuration and all data
    /// </summary>
    System = 0,

    /// <summary>
    /// Employee account (HR employees)
    /// Access is scoped based on department/company/position
    /// </summary>
    Employee = 1
}

/// <summary>
/// Account status enumeration
/// </summary>
public enum AccountStatus
{
    /// <summary>
    /// Account is pending activation
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Account is active and can login
    /// </summary>
    Active = 1,

    /// <summary>
    /// Account is suspended (temporarily disabled)
    /// </summary>
    Suspended = 2,

    /// <summary>
    /// Account is deactivated (permanently disabled)
    /// </summary>
    Deactivated = 3
}

/// <summary>
/// Unified Account entity for authentication
/// Supports both System accounts and Employee accounts
///
/// Architecture:
/// - Account: Authentication data (login, password, sessions)
/// - SystemProfile: Additional data for System accounts (roles, permissions)
/// - EmployeeProfile: Link to Employee entity for Employee accounts
///
/// This replaces the separate Operator entity for a unified auth experience.
/// </summary>
public class Account : Entity, IAuditableEntity
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

    // Audit fields
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

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
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            PasswordHash = passwordHash,
            FullName = fullName,
            PhoneNumber = phoneNumber,
            AccountType = AccountType.System,
            Status = AccountStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };

        return account;
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
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            PasswordHash = passwordHash,
            FullName = fullName,
            PhoneNumber = phoneNumber,
            AccountType = AccountType.Employee,
            Status = AccountStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };

        return account;
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
        ModifiedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Suspend the account
    /// </summary>
    public void Suspend()
    {
        Status = AccountStatus.Suspended;
        ModifiedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Deactivate the account
    /// </summary>
    public void Deactivate()
    {
        Status = AccountStatus.Deactivated;
        ModifiedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Record successful login
    /// </summary>
    public void RecordLogin()
    {
        LastLoginAtUtc = DateTime.UtcNow;
        FailedLoginAttempts = 0;
        LockedUntilUtc = null;
        ModifiedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Record failed login attempt
    /// </summary>
    public void RecordFailedLogin(int maxAttempts = 5, int lockoutMinutes = 30)
    {
        FailedLoginAttempts++;
        ModifiedAtUtc = DateTime.UtcNow;

        if (FailedLoginAttempts >= maxAttempts)
        {
            LockedUntilUtc = DateTime.UtcNow.AddMinutes(lockoutMinutes);
        }
    }

    /// <summary>
    /// Check if account is locked
    /// </summary>
    public bool IsLocked()
    {
        if (!LockedUntilUtc.HasValue)
            return false;

        if (LockedUntilUtc.Value <= DateTime.UtcNow)
        {
            // Lockout expired, reset
            LockedUntilUtc = null;
            FailedLoginAttempts = 0;
            return false;
        }

        return true;
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
        ModifiedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Update profile information
    /// </summary>
    public void UpdateProfile(string fullName, string? phoneNumber)
    {
        FullName = fullName;
        PhoneNumber = phoneNumber;
        ModifiedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Enable two-factor authentication
    /// </summary>
    public void EnableTwoFactor(string secretKey)
    {
        IsTwoFactorEnabled = true;
        TwoFactorSecretKey = secretKey;
        ModifiedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Disable two-factor authentication
    /// </summary>
    public void DisableTwoFactor()
    {
        IsTwoFactorEnabled = false;
        TwoFactorSecretKey = null;
        ModifiedAtUtc = DateTime.UtcNow;
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
