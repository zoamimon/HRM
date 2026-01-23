using HRM.BuildingBlocks.Domain.Abstractions.Results;
using HRM.BuildingBlocks.Domain.Enums;
using HRM.Modules.Identity.Application.Errors;
using HRM.Modules.Identity.Domain.Repositories;
using MediatR;

namespace HRM.Modules.Identity.Application.Commands.RevokeSession;

/// <summary>
/// Handler for RevokeSessionCommand
/// Revokes specific session (logout from specific device)
///
/// Dependencies:
/// - IRefreshTokenRepository: Access RefreshTokens and commit
///
/// Security Logic:
/// 1. Find session by ID
/// 2. Verify session belongs to current operator
/// 3. Revoke if active
/// 4. Idempotent (no error if already revoked)
///
/// Security Features:
/// - Ownership verification (OperatorId check)
/// - Cannot revoke other users' sessions
/// - Audit trail (IP tracking)
///
/// Performance:
/// - Single query (indexed by primary key)
/// - Fast operation (~1-3ms)
/// </summary>
public sealed class RevokeSessionCommandHandler
    : IRequestHandler<RevokeSessionCommand, Result>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;

    public RevokeSessionCommandHandler(IRefreshTokenRepository refreshTokenRepository)
    {
        _refreshTokenRepository = refreshTokenRepository;
    }

    public async Task<Result> Handle(
        RevokeSessionCommand request,
        CancellationToken cancellationToken)
    {
        // Find session by ID
        var session = await _refreshTokenRepository.GetByIdAsync(
            request.SessionId,
            cancellationToken);

        // If not found, return not found error
        if (session is null)
        {
            return Result.Failure(SessionErrors.NotFound());
        }

        // Security: Verify session belongs to current operator (polymorphic design)
        if (session.UserType != UserType.Operator || session.PrincipalId != request.OperatorId)
        {
            return Result.Failure(SessionErrors.UnauthorizedAccess());
        }

        // If already revoked, return success (idempotent)
        if (session.RevokedAt.HasValue)
        {
            return Result.Success();
        }

        // Revoke the session
        session.Revoke(request.IpAddress);

        // UnitOfWorkBehavior will commit
        return Result.Success();
    }
}
