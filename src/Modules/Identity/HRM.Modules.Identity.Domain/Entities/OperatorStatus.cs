namespace HRM.Modules.Identity.Domain.Entities;

/// <summary>
/// Represents the status of an Operator account in the system
/// Controls operator lifecycle and access permissions
///
/// Status Flow:
/// 1. Pending → Active (manual activation by admin)
/// 2. Active → Suspended (temporary disable)
/// 3. Suspended → Active (reactivation)
/// 4. Active → Deactivated (permanent disable)
///
/// Business Rules:
/// - Only Active operators can login
/// - Pending operators must be activated by admin
/// - Suspended operators can be reactivated
/// - Deactivated is final state (soft delete preferred for audit)
/// </summary>
public enum OperatorStatus
{
    /// <summary>
    /// Operator registered but not yet activated
    /// Cannot login until activated by admin
    /// Initial state after registration
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Operator is active and can login
    /// Normal operational state
    /// </summary>
    Active = 1,

    /// <summary>
    /// Operator temporarily suspended
    /// Cannot login, can be reactivated
    /// Used for temporary access restrictions
    /// </summary>
    Suspended = 2,

    /// <summary>
    /// Operator permanently deactivated
    /// Cannot login, typically not reactivated
    /// Consider soft delete instead for audit trail
    /// </summary>
    Deactivated = 3
}
