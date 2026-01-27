using HRM.Modules.Identity.Domain.Entities;

namespace HRM.Modules.Identity.Application.Queries.GetOperators;

/// <summary>
/// Lightweight DTO for operator list view
/// Contains only essential fields for display in list/table
///
/// Excluded Fields (for performance):
/// - PasswordHash (security)
/// - TwoFactorSecret (security)
/// - FailedLoginAttempts (detail view)
/// - LockedUntilUtc (detail view)
/// </summary>
public sealed record OperatorSummaryDto
{
    /// <summary>
    /// Operator unique identifier
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Login username
    /// </summary>
    public required string Username { get; init; }

    /// <summary>
    /// Email address
    /// </summary>
    public required string Email { get; init; }

    /// <summary>
    /// Display name
    /// </summary>
    public required string FullName { get; init; }

    /// <summary>
    /// Current account status (Pending, Active, Suspended, Deactivated)
    /// </summary>
    public required OperatorStatus Status { get; init; }

    /// <summary>
    /// When the operator was created
    /// </summary>
    public required DateTime CreatedAtUtc { get; init; }

    /// <summary>
    /// Last successful login timestamp (null if never logged in)
    /// </summary>
    public DateTime? LastLoginAtUtc { get; init; }
}
