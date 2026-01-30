using HRM.BuildingBlocks.Domain.Entities;
using HRM.Modules.Identity.Domain.Enums;
using HRM.Modules.Identity.Domain.Entities;

namespace HRM.Modules.Identity.Domain.Repositories;

/// <summary>
/// Repository interface for RefreshToken entity
/// Defines data access contract for session and token management
///
/// Design Patterns:
/// - Repository Pattern: Abstracts data access from domain
/// - Unit of Work: Add/Update/Revoke don't save immediately (use UnitOfWork.CommitAsync)
///
/// Implementation Notes:
/// - All async methods for scalability
/// - CancellationToken support for long-running queries
/// - No IQueryable exposure (keeps domain pure)
/// - Returns null for not found (use Result pattern in Application layer)
///
/// Query Methods:
/// - GetByTokenAsync: Find token by token string (for RefreshToken command)
/// - GetByIdAsync: Find token by ID
/// - GetActiveSessionsByOperatorIdAsync: Get all active sessions for operator
///
/// Command Methods:
/// - Add: Create new refresh token (INSERT)
/// - Update: Modify existing token (UPDATE) - EF tracks changes automatically
/// - Remove: Delete token (hard delete)
///
/// Usage Example (Application Layer):
/// <code>
/// // In RefreshTokenCommandHandler
/// var existingToken = await _refreshTokenRepository.GetByTokenAsync(
///     request.RefreshToken,
///     cancellationToken
/// );
///
/// if (existingToken is null || !existingToken.IsActive)
///     return Result.Failure(AuthenticationErrors.InvalidRefreshToken());
///
/// existingToken.Revoke(request.IpAddress, newToken);
/// var newTokenEntity = RefreshToken.Create(...);
/// _refreshTokenRepository.Add(newTokenEntity);
/// await _unitOfWork.CommitAsync(cancellationToken);
/// </code>
/// </summary>
public interface IRefreshTokenRepository
{
    /// <summary>
    /// Get refresh token by ID
    /// Returns null if not found
    ///
    /// Use Cases:
    /// - Internal lookups
    /// - Admin token management
    /// </summary>
    /// <param name="id">Token ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>RefreshToken entity or null if not found</returns>
    Task<RefreshToken?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get refresh token by token string
    /// Returns null if not found
    ///
    /// Use Cases:
    /// - RefreshToken command (validate and rotate token)
    /// - Token validation
    ///
    /// Performance:
    /// - Indexed column (see RefreshTokenConfiguration)
    /// - Typical execution time: 2-10ms
    /// </summary>
    /// <param name="token">Token string</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>RefreshToken entity or null if not found</returns>
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get refresh token by token string and principal (user type + ID)
    /// Returns null if not found or doesn't belong to principal
    ///
    /// Use Cases:
    /// - RevokeAllSessionsExceptCurrent command (verify current token ownership)
    /// - Security checks
    ///
    /// Performance:
    /// - Uses composite condition for security
    /// - Indexed token lookup
    /// </summary>
    /// <param name="token">Token string</param>
    /// <param name="accountType">Type of account (System or Employee)</param>
    /// <param name="principalId">Principal ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>RefreshToken entity or null if not found</returns>
    Task<RefreshToken?> GetByTokenAndPrincipalAsync(
        string token,
        AccountType accountType,
        Guid principalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get refresh token by token string and principal (deprecated - use AccountType overload).
    /// </summary>
    [Obsolete("Use GetByTokenAndPrincipalAsync(string, AccountType, Guid, CancellationToken) instead")]
#pragma warning disable CS0618
    Task<RefreshToken?> GetByTokenAndPrincipalAsync(
        string token,
        UserType userType,
        Guid principalId,
        CancellationToken cancellationToken = default);
#pragma warning restore CS0618

    /// <summary>
    /// Get all active sessions for a principal (except specified token)
    /// Returns empty list if no active sessions found
    ///
    /// Use Cases:
    /// - RevokeAllSessionsExceptCurrent command
    /// - Session management UI (show all devices)
    ///
    /// Active Session Criteria:
    /// - RevokedAt is NULL (not revoked)
    /// - ExpiresAt > NOW (not expired)
    /// - Excludes token with specified ID
    ///
    /// Performance:
    /// - Indexed (UserType, PrincipalId) lookup
    /// - Filtered in database (not in memory)
    /// - Typical execution time: 5-20ms for 1-100 sessions
    /// </summary>
    /// <param name="accountType">Type of account (System or Employee)</param>
    /// <param name="principalId">Principal ID</param>
    /// <param name="exceptTokenId">Token ID to exclude (current session)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of active RefreshToken entities</returns>
    Task<List<RefreshToken>> GetActiveSessionsExceptAsync(
        AccountType accountType,
        Guid principalId,
        Guid exceptTokenId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get active sessions except specified (deprecated - use AccountType overload).
    /// </summary>
    [Obsolete("Use GetActiveSessionsExceptAsync(AccountType, Guid, Guid, CancellationToken) instead")]
#pragma warning disable CS0618
    Task<List<RefreshToken>> GetActiveSessionsExceptAsync(
        UserType userType,
        Guid principalId,
        Guid exceptTokenId,
        CancellationToken cancellationToken = default);
#pragma warning restore CS0618

    /// <summary>
    /// Get all active sessions for a principal
    /// Returns empty list if no active sessions found
    ///
    /// Use Cases:
    /// - GetActiveSessions query (display all sessions in UI)
    /// - Session monitoring
    /// - Security audit
    ///
    /// Active Session Criteria:
    /// - RevokedAt is NULL (not revoked)
    /// - ExpiresAt > NOW (not expired)
    ///
    /// Performance:
    /// - Indexed (UserType, PrincipalId) lookup
    /// - Filtered in database (not in memory)
    /// - Ordered by CreatedAtUtc DESC (most recent first)
    /// - Typical execution time: 5-20ms for 1-100 sessions
    /// </summary>
    /// <param name="accountType">Type of account (System or Employee)</param>
    /// <param name="principalId">Principal ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of active RefreshToken entities ordered by creation date (newest first)</returns>
    Task<List<RefreshToken>> GetActiveSessionsAsync(
        AccountType accountType,
        Guid principalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get active sessions (deprecated - use AccountType overload).
    /// </summary>
    [Obsolete("Use GetActiveSessionsAsync(AccountType, Guid, CancellationToken) instead")]
#pragma warning disable CS0618
    Task<List<RefreshToken>> GetActiveSessionsAsync(
        UserType userType,
        Guid principalId,
        CancellationToken cancellationToken = default);
#pragma warning restore CS0618

    /// <summary>
    /// Add new refresh token to repository
    /// Does NOT save to database immediately (use UnitOfWork.CommitAsync)
    ///
    /// Use Cases:
    /// - Login command (create new session)
    /// - RefreshToken command (create rotated token)
    ///
    /// Usage:
    /// <code>
    /// var refreshToken = RefreshToken.Create(...);
    /// _refreshTokenRepository.Add(refreshToken);
    /// await _unitOfWork.CommitAsync(cancellationToken);
    /// </code>
    /// </summary>
    /// <param name="refreshToken">RefreshToken entity to add</param>
    void Add(RefreshToken refreshToken);

    /// <summary>
    /// Update existing refresh token
    /// Does NOT save to database immediately (use UnitOfWork.CommitAsync)
    ///
    /// Note: With EF Core change tracking, explicit Update() call is usually not needed
    /// Just modify the entity properties (e.g., token.Revoke()) and CommitAsync will detect changes
    ///
    /// Use Cases:
    /// - Revoke token (token.Revoke())
    /// - Update token metadata
    ///
    /// Usage:
    /// <code>
    /// var token = await _refreshTokenRepository.GetByTokenAsync(tokenString, cancellationToken);
    /// token.Revoke(ipAddress);
    /// // No need to call Update() - EF tracks changes
    /// await _unitOfWork.CommitAsync(cancellationToken);
    /// </code>
    /// </summary>
    /// <param name="refreshToken">RefreshToken entity to update</param>
    void Update(RefreshToken refreshToken);

    /// <summary>
    /// Remove refresh token from repository (hard delete)
    /// Does NOT save to database immediately (use UnitOfWork.CommitAsync)
    ///
    /// Note: RefreshTokens are hard deleted, not soft deleted
    /// Expired tokens should be cleaned up periodically
    ///
    /// Use Cases:
    /// - Logout command (delete token)
    /// - Cleanup expired tokens (background job)
    ///
    /// Usage:
    /// <code>
    /// var token = await _refreshTokenRepository.GetByTokenAsync(tokenString, cancellationToken);
    /// _refreshTokenRepository.Remove(token);
    /// await _unitOfWork.CommitAsync(cancellationToken);
    /// </code>
    /// </summary>
    /// <param name="refreshToken">RefreshToken entity to remove</param>
    void Remove(RefreshToken refreshToken);
}
