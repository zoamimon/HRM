using HRM.BuildingBlocks.Domain.Abstractions.Results;
using HRM.BuildingBlocks.Domain.Abstractions.UnitOfWork;
using HRM.Modules.Identity.Application.Errors;
using HRM.Modules.Identity.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Modules.Identity.Application.Commands.RevokeAllSessionsExceptCurrent;

/// <summary>
/// Handler for RevokeAllSessionsExceptCurrentCommand
/// Revokes all operator's sessions except current device
///
/// Dependencies:
/// - IModuleUnitOfWork: Access RefreshTokens and commit
///
/// Business Logic:
/// 1. Verify current token exists and belongs to operator
/// 2. Find all active sessions for operator
/// 3. Filter out current session
/// 4. Revoke all others in bulk
/// 5. Return count of revoked sessions
///
/// Security Features:
/// - Requires valid current token (cannot accidentally logout self)
/// - Ownership verification (OperatorId check)
/// - Atomic transaction (all or nothing)
/// - Audit trail for each revocation
///
/// Performance:
/// - Single query to load sessions
/// - Bulk revocation in memory
/// - Single transaction commit
/// - Efficient for 1-100 sessions
/// </summary>
public sealed class RevokeAllSessionsExceptCurrentCommandHandler
    : IRequestHandler<RevokeAllSessionsExceptCurrentCommand, Result<RevokeAllSessionsResult>>
{
    private readonly IModuleUnitOfWork _unitOfWork;

    public RevokeAllSessionsExceptCurrentCommandHandler(IModuleUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RevokeAllSessionsResult>> Handle(
        RevokeAllSessionsExceptCurrentCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Verify current token exists and belongs to operator
        var currentToken = await _unitOfWork.Set<RefreshToken>()
            .SingleOrDefaultAsync(
                rt => rt.Token == request.CurrentRefreshToken &&
                      rt.OperatorId == request.OperatorId,
                cancellationToken);

        if (currentToken is null)
        {
            return Result<RevokeAllSessionsResult>.Failure(
                SessionErrors.CannotIdentifyCurrentSession());
        }

        // 2. Find all active sessions for operator (except current)
        var sessionsToRevoke = await _unitOfWork.Set<RefreshToken>()
            .Where(rt =>
                rt.OperatorId == request.OperatorId &&
                rt.Id != currentToken.Id && // Exclude current
                rt.RevokedAt == null &&     // Only active
                rt.ExpiresAt > DateTime.UtcNow) // Not expired
            .ToListAsync(cancellationToken);

        // 3. If no sessions to revoke, return success
        if (sessionsToRevoke.Count == 0)
        {
            return Result<RevokeAllSessionsResult>.Success(new RevokeAllSessionsResult
            {
                RevokedCount = 0,
                Message = "No other active sessions found"
            });
        }

        // 4. Revoke all sessions
        foreach (var session in sessionsToRevoke)
        {
            session.Revoke(request.IpAddress);
        }

        // UnitOfWorkBehavior will commit

        // 5. Return result
        var message = sessionsToRevoke.Count == 1
            ? "1 session was terminated"
            : $"{sessionsToRevoke.Count} sessions were terminated";

        return Result<RevokeAllSessionsResult>.Success(new RevokeAllSessionsResult
        {
            RevokedCount = sessionsToRevoke.Count,
            Message = message
        });
    }
}
