using HRM.BuildingBlocks.Domain.Abstractions.Results;
using HRM.Modules.Identity.Domain.Enums;
using HRM.Modules.Identity.Application.Abstractions.Authentication;
using HRM.Modules.Identity.Application.Commands.Login;
using HRM.Modules.Identity.Application.Configuration;
using HRM.Modules.Identity.Application.Errors;
using HRM.Modules.Identity.Domain.Entities;
using HRM.Modules.Identity.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Options;

namespace HRM.Modules.Identity.Application.Commands.RefreshToken;

/// <summary>
/// Handler for RefreshTokenCommand.
/// Implements token rotation pattern for enhanced security.
/// Uses Account entity (unified login) instead of legacy Operator.
///
/// Token Rotation Pattern:
/// - Old token → Revoke (set RevokedAt, ReplacedByToken)
/// - New token → Generate and store
/// - Access token → Always generate fresh
/// </summary>
public sealed class RefreshTokenCommandHandler
    : IRequestHandler<RefreshTokenCommand, Result<LoginResponse>>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ITokenService _tokenService;
    private readonly JwtOptions _jwtOptions;

    public RefreshTokenCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        IAccountRepository accountRepository,
        ITokenService tokenService,
        IOptions<JwtOptions> jwtOptions)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _accountRepository = accountRepository;
        _tokenService = tokenService;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<Result<LoginResponse>> Handle(
        RefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Find and validate refresh token
        var existingToken = await _refreshTokenRepository.GetByTokenAsync(
            request.RefreshToken,
            cancellationToken);

        if (existingToken is null)
        {
            return Result.Failure<LoginResponse>(
                AuthenticationErrors.InvalidRefreshToken());
        }

        // 2. Check if token is active (not revoked, not expired)
        if (existingToken.RevokedAt.HasValue)
        {
            return Result.Failure<LoginResponse>(
                AuthenticationErrors.RefreshTokenRevoked());
        }

        if (existingToken.IsExpired)
        {
            return Result.Failure<LoginResponse>(
                AuthenticationErrors.RefreshTokenExpired());
        }

        // 3. Fetch account using PrincipalId
        var account = await _accountRepository.GetByIdAsync(
            existingToken.PrincipalId,
            cancellationToken);

        if (account is null)
        {
            return Result.Failure<LoginResponse>(
                AuthenticationErrors.InvalidRefreshToken());
        }

        // 4. Validate account status
        if (!account.CanLogin())
        {
            if (account.Status == AccountStatus.Suspended)
            {
                return Result.Failure<LoginResponse>(
                    AuthenticationErrors.AccountSuspended());
            }

            return Result.Failure<LoginResponse>(
                AuthenticationErrors.AccountNotActive());
        }

        // 5. Check if account is locked
        if (account.IsLocked())
        {
            return Result.Failure<LoginResponse>(
                AuthenticationErrors.AccountLockedOut(account.LockedUntilUtc));
        }

        // 6. Generate new access token
        var accessTokenResult = _tokenService.GenerateAccessToken(account);

        // 7. Generate new refresh token (inherit same expiry duration)
        var originalExpiryDuration = existingToken.ExpiresAt - existingToken.CreatedAtUtc;
        var newRefreshTokenExpiry = DateTime.UtcNow.Add(originalExpiryDuration);
        var newRefreshToken = _tokenService.GenerateRefreshToken(newRefreshTokenExpiry);

        // 8. Revoke old token (token rotation)
        existingToken.Revoke(request.IpAddress, newRefreshToken);

        // 9. Store new refresh token
        var newRefreshTokenEntity = Domain.Entities.RefreshToken.Create(
            account.AccountType,
            account.Id,
            newRefreshToken,
            newRefreshTokenExpiry,
            request.IpAddress,
            request.UserAgent
        );

        _refreshTokenRepository.Add(newRefreshTokenEntity);
        // UnitOfWorkBehavior will commit

        // 10. Build and return response
        var response = new LoginResponse
        {
            AccessToken = accessTokenResult.Token,
            RefreshToken = newRefreshToken,
            AccessTokenExpiry = accessTokenResult.ExpiresAt,
            RefreshTokenExpiry = newRefreshTokenExpiry,
            User = new UserInfo
            {
                Id = account.Id,
                Username = account.Username,
                Email = account.Email,
                FullName = account.FullName
            }
        };

        return Result.Success(response);
    }
}
