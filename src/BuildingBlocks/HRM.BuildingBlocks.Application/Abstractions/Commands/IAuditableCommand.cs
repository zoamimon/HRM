namespace HRM.BuildingBlocks.Application.Abstractions.Commands;

/// <summary>
/// Marker interface for commands that require automatic audit logging.
///
/// Purpose:
/// - Identifies commands that should have IP address and User Agent automatically injected
/// - Used by AuditBehavior in the MediatR pipeline
/// - Simplifies command creation by removing repetitive audit parameter passing
///
/// How It Works:
/// 1. Command implements IAuditableCommand
/// 2. AuditBehavior detects interface in pipeline
/// 3. Behavior extracts IP/UserAgent from IClientInfoService
/// 4. Values are injected into command properties before handler execution
///
/// Requirements for Commands:
/// - Must have properties with init/set accessors:
///   - string? IpAddress { get; init; } or { get; set; }
///   - string? UserAgent { get; init; } or { get; set; }
/// - Properties can be nullable (string?)
///
/// Example Usage:
/// <code>
/// // Before (manual IP/UserAgent passing):
/// public sealed record LogoutCommand(
///     string RefreshToken,
///     string? IpAddress) : IModuleCommand;
///
/// // After (automatic injection):
/// public sealed record LogoutCommand(
///     string RefreshToken) : IModuleCommand, IAuditableCommand
/// {
///     public string? IpAddress { get; init; }
///     public string? UserAgent { get; init; }
/// }
/// </code>
///
/// Benefits:
/// - Consistent audit logging across all commands
/// - Removes audit parameters from API endpoints
/// - Centralized audit logic in behavior
/// - No manual IClientInfoService injection in endpoints
///
/// When to Use:
/// ✅ Commands that need audit trail (login, logout, revoke session)
/// ✅ Commands that modify security-sensitive data
/// ✅ Commands that perform user actions requiring tracking
///
/// When NOT to Use:
/// ❌ Commands where IP/UserAgent are business requirements (e.g., RefreshToken entity)
/// ❌ Read-only queries (use IAuditableCommand only on commands)
/// ❌ Commands that don't need audit trail
///
/// Pipeline Flow:
/// 1. Command created without IP/UserAgent
/// 2. AuditBehavior: Detects IAuditableCommand → injects values
/// 3. ValidationBehavior: Validates command
/// 4. CommandHandler: Executes with audit data populated
/// 5. UnitOfWorkBehavior: Commits changes
/// </summary>
public interface IAuditableCommand
{
    /// <summary>
    /// IP address from which the command was issued.
    /// Automatically injected by AuditBehavior from IClientInfoService.
    /// Must use 'set' accessor for runtime injection.
    /// </summary>
    string? IpAddress { get; set; }

    /// <summary>
    /// User agent (browser/device) that issued the command.
    /// Automatically injected by AuditBehavior from IClientInfoService.
    /// Must use 'set' accessor for runtime injection.
    /// </summary>
    string? UserAgent { get; set; }
}
