using HRM.BuildingBlocks.Domain.Abstractions.Results;
using HRM.Modules.Identity.Domain.Repositories;
using MediatR;

namespace HRM.Modules.Identity.Application.Queries.GetActiveSessions;

/// <summary>
/// Handler for GetActiveSessionsQuery
/// Returns list of active sessions for operator
///
/// Dependencies:
/// - IRefreshTokenRepository: Access RefreshTokens
///
/// Query Logic:
/// 1. Find all refresh tokens for operator
/// 2. Filter active only (not revoked, not expired)
/// 3. Sort by most recent first
/// 4. Mark current session if token provided
/// 5. Project to SessionInfo DTO
///
/// Performance:
/// - Indexed query (composite index on OperatorId, RevokedAt, ExpiresAt)
/// - Small result set (1-10 sessions typically)
/// - No joins needed (all data in RefreshTokens)
/// - Fast query (~1-5ms)
///
/// Security:
/// - Only returns sessions for specified OperatorId
/// - Cannot access other users' sessions
/// - No sensitive data in response
/// </summary>
public sealed class GetActiveSessionsQueryHandler
    : IRequestHandler<GetActiveSessionsQuery, Result<List<SessionInfo>>>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;

    public GetActiveSessionsQueryHandler(IRefreshTokenRepository refreshTokenRepository)
    {
        _refreshTokenRepository = refreshTokenRepository;
    }

    public async Task<Result<List<SessionInfo>>> Handle(
        GetActiveSessionsQuery request,
        CancellationToken cancellationToken)
    {
        // Query active sessions from repository
        var activeTokens = await _refreshTokenRepository.GetActiveSessionsByOperatorIdAsync(
            request.OperatorId,
            cancellationToken);

        // Project to SessionInfo DTOs
        var activeSessions = activeTokens.Select(rt => new SessionInfo
        {
            Id = rt.Id,
            CreatedAt = rt.CreatedAtUtc,
            ExpiresAt = rt.ExpiresAt,
            UserAgent = rt.UserAgent,
            CreatedByIp = rt.CreatedByIp,
            IsCurrent = rt.Token == request.CurrentRefreshToken
        }).ToList();

        return Result.Success(activeSessions);
    }
}
