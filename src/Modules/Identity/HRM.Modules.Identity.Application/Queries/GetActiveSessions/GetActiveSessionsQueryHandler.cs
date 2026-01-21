using HRM.BuildingBlocks.Domain.Abstractions.Results;
using HRM.BuildingBlocks.Domain.Abstractions.UnitOfWork;
using HRM.Modules.Identity.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Modules.Identity.Application.Queries.GetActiveSessions;

/// <summary>
/// Handler for GetActiveSessionsQuery
/// Returns list of active sessions for operator
///
/// Dependencies:
/// - IModuleUnitOfWork: Access RefreshTokens DbSet
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
    private readonly IModuleUnitOfWork _unitOfWork;

    public GetActiveSessionsQueryHandler(IModuleUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<List<SessionInfo>>> Handle(
        GetActiveSessionsQuery request,
        CancellationToken cancellationToken)
    {
        // Query active sessions
        var activeSessions = await _unitOfWork.Set<RefreshToken>()
            .Where(rt =>
                rt.OperatorId == request.OperatorId &&
                rt.RevokedAt == null &&
                rt.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(rt => rt.CreatedAtUtc)
            .Select(rt => new SessionInfo
            {
                Id = rt.Id,
                CreatedAt = rt.CreatedAtUtc,
                ExpiresAt = rt.ExpiresAt,
                UserAgent = rt.UserAgent,
                CreatedByIp = rt.CreatedByIp,
                IsCurrent = rt.Token == request.CurrentRefreshToken
            })
            .ToListAsync(cancellationToken);

        return Result<List<SessionInfo>>.Success(activeSessions);
    }
}
