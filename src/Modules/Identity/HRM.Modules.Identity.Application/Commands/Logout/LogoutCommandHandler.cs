using HRM.BuildingBlocks.Domain.Abstractions.Results;
using HRM.Modules.Identity.Domain.Repositories;
using MediatR;

namespace HRM.Modules.Identity.Application.Commands.Logout;

/// <summary>
/// Handler for LogoutCommand
/// Revokes refresh token to terminate session
///
/// Dependencies:
/// - IRefreshTokenRepository: Access RefreshTokens and commit changes
///
/// Business Logic:
/// 1. Find refresh token in database
/// 2. If found and active → Revoke it
/// 3. If not found or already revoked → Return success (idempotent)
///
/// Idempotency:
/// - Multiple logout calls have same effect
/// - No error if token doesn't exist
/// - No error if token already revoked
/// - User-friendly behavior
///
/// Performance:
/// - Single database query (indexed Token column)
/// - Fast operation (~1-5ms)
///
/// Security:
/// - Only revokes one specific token (this device)
/// - Other devices remain logged in
/// - For logout all devices, use RevokeAllSessionsCommand
/// </summary>
public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;

    public LogoutCommandHandler(IRefreshTokenRepository refreshTokenRepository)
    {
        _refreshTokenRepository = refreshTokenRepository;
    }

    public async Task<Result> Handle(
        LogoutCommand request,
        CancellationToken cancellationToken)
    {
        // Find refresh token
        var refreshToken = await _refreshTokenRepository.GetByTokenAsync(
            request.RefreshToken,
            cancellationToken);

        // If not found or already revoked → success (idempotent)
        if (refreshToken == null || refreshToken.RevokedAt.HasValue)
        {
            return Result.Success();
        }

        // Revoke the token
        refreshToken.Revoke(request.IpAddress);

        // UnitOfWorkBehavior will commit
        return Result.Success();
    }
}
