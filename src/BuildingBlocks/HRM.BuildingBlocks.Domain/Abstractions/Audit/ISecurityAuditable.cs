namespace HRM.BuildingBlocks.Domain.Abstractions.Audit;

/// <summary>
/// Interface for security-critical entities that require enhanced audit trail.
///
/// This is DIFFERENT from IAuditableEntity:
/// - IAuditableEntity: Business audit (who created/modified a record)
/// - ISecurityAuditable: Security audit (when security-sensitive changes occurred)
///
/// Security-sensitive events tracked:
/// - Password changes (credential rotation, breach response)
/// - Failed login attempts (brute force detection)
/// - 2FA enablement/disablement (security posture changes)
/// - Status changes (account suspension, deactivation)
///
/// Use Case: Account entity (and potentially other security entities)
///
/// Compliance:
/// - SOC2: Requires tracking of authentication events
/// - GDPR: Data subject access requests may include this info
/// - ISO 27001: Security event logging requirements
/// </summary>
public interface ISecurityAuditable
{
    /// <summary>
    /// When the password was last changed (UTC).
    /// NULL if password never changed since account creation.
    ///
    /// Use Cases:
    /// - Password expiry policies (force change every N days)
    /// - Security investigations (when was password compromised?)
    /// - Compliance reporting
    /// </summary>
    DateTime? PasswordChangedAtUtc { get; }

    /// <summary>
    /// When the last failed login attempt occurred (UTC).
    /// NULL if no failed attempts recorded.
    ///
    /// Use Cases:
    /// - Brute force detection (many failures in short time)
    /// - Account lockout decisions
    /// - Security dashboards (recent failed attempts)
    /// </summary>
    DateTime? LastFailedLoginAtUtc { get; }

    /// <summary>
    /// When two-factor authentication was enabled (UTC).
    /// NULL if 2FA is not enabled or was never enabled.
    ///
    /// Use Cases:
    /// - Security posture reporting (% accounts with 2FA)
    /// - Compliance audits (when was 2FA enabled?)
    /// - Security investigations (was 2FA disabled before breach?)
    /// </summary>
    DateTime? TwoFactorChangedAtUtc { get; }

    /// <summary>
    /// When the account status was last changed (UTC).
    /// NULL if status never changed since creation.
    ///
    /// Status changes include:
    /// - Pending → Active (activation)
    /// - Active → Suspended (temporary disable)
    /// - Active → Deactivated (permanent disable)
    /// - Suspended → Active (reactivation)
    ///
    /// Use Cases:
    /// - Admin action auditing (who suspended when?)
    /// - Security investigations (was account disabled before incident?)
    /// - Compliance reporting
    /// </summary>
    DateTime? StatusChangedAtUtc { get; }
}
