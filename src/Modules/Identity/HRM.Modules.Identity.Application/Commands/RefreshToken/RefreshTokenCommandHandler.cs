using HRM.BuildingBlocks.Domain.Abstractions.Results;
using HRM.Modules.Identity.Application.Abstractions.Authentication;
using HRM.Modules.Identity.Application.Commands.Login;
using HRM.Modules.Identity.Application.Errors;
using HRM.Modules.Identity.Domain.Entities;
using HRM.Modules.Identity.Infrastructure.Authentication;
using HRM.Modules.Identity.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HRM.Modules.Identity.Application.Commands.RefreshToken;

/// <summary>
/// Handler for RefreshTokenCommand
/// Implements token rotation pattern for enhanced security
///
/// Dependencies:
/// - IdentityDbContext: Access RefreshTokens and commit
/// - ITokenService: Generate new tokens
/// - JwtOptions: Get token expiry configuration
///
/// Token Rotation Pattern:
/// - Old token → Revoke (set RevokedAt, ReplacedByToken)
/// - New token → Generate and store
/// - Access token → Always generate fresh
/// - Creates audit chain for security
///
/// Security Features:
/// - Detects token reuse (revoked token used again)
/// - Limits token lifetime
/// - Operator status revalidation
/// - IP and UserAgent tracking
///
/// Performance:
/// - Single query with Include (operator data)
/// - Fast token generation (~1-3ms)
/// - Single transaction commit
///
/// Error Handling:
/// - Invalid/expired/revoked token → 401 Unauthorized
/// - Operator not active → 403 Forbidden
/// - Database errors → Propagated
/// </summary>
public sealed class RefreshTokenCommandHandler
    : IRequestHandler<RefreshTokenCommand, Result<LoginResponse>>
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly JwtOptions _jwtOptions;

    public RefreshTokenCommandHandler(
        IdentityDbContext dbContext,
        ITokenService tokenService,
        IOptions<JwtOptions> jwtOptions)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<Result<LoginResponse>> Handle(
        RefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Find and validate refresh token (with Operator data)
        var existingToken = await _dbContext.RefreshTokens
            .Include(rt => rt.Operator)
            .SingleOrDefaultAsync(
                rt => rt.Token == request.RefreshToken,
                cancellationToken);

        if (existingToken is null)
        {
            return Result<LoginResponse>.Failure(
                AuthenticationErrors.InvalidRefreshToken());
        }

        // 2. Check if token is active (not revoked, not expired)
        if (existingToken.RevokedAt.HasValue)
        {
            return Result<LoginResponse>.Failure(
                AuthenticationErrors.RefreshTokenRevoked());
        }

        if (existingToken.IsExpired)
        {
            return Result<LoginResponse>.Failure(
                AuthenticationErrors.RefreshTokenExpired());
        }

        // 3. Get operator and validate status
        var @operator = existingToken.Operator;

        if (@operator.Status != OperatorStatus.Active)
        {
            if (@operator.Status == OperatorStatus.Suspended)
            {
                return Result<LoginResponse>.Failure(
                    AuthenticationErrors.AccountSuspended());
            }

            return Result<LoginResponse>.Failure(
                AuthenticationErrors.AccountNotActive());
        }

        // 4. Check if account is locked
        if (@operator.LockedUntilUtc.HasValue && @operator.LockedUntilUtc.Value > DateTime.UtcNow)
        {
            return Result<LoginResponse>.Failure(
                AuthenticationErrors.AccountLockedOut(@operator.LockedUntilUtc));
        }

        // 5. Generate new access token
        var accessTokenResult = _tokenService.GenerateAccessToken(@operator);

        // 6. Generate new refresh token (inherit same expiry duration)
        var originalExpiryDuration = existingToken.ExpiresAt - existingToken.CreatedAtUtc;
        var newRefreshTokenExpiry = DateTime.UtcNow.Add(originalExpiryDuration);
        var newRefreshToken = _tokenService.GenerateRefreshToken(newRefreshTokenExpiry);

        // 7. Revoke old token (token rotation)
        existingToken.Revoke(request.IpAddress, newRefreshToken);

        // 8. Store new refresh token
        var newRefreshTokenEntity = Domain.Entities.RefreshToken.Create(
            @operator.Id,
            newRefreshToken,
            newRefreshTokenExpiry,
            request.IpAddress,
            request.UserAgent
        );

        _dbContext.RefreshTokens.Add(newRefreshTokenEntity);
        // UnitOfWorkBehavior will commit

        // 9. Build and return response
        var response = new LoginResponse
        {
            AccessToken = accessTokenResult.Token,
            RefreshToken = newRefreshToken,
            AccessTokenExpiry = accessTokenResult.ExpiresAt,
            RefreshTokenExpiry = newRefreshTokenExpiry,
            User = new UserInfo
            {
                Id = @operator.Id,
                Username = @operator.Username,
                Email = @operator.Email,
                FullName = @operator.FullName
            }
        };

        return Result<LoginResponse>.Success(response);
    }
}
