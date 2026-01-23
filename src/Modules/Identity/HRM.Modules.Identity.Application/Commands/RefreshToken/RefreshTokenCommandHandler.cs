using HRM.BuildingBlocks.Domain.Abstractions.Results;
using HRM.BuildingBlocks.Domain.Enums;
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
/// Handler for RefreshTokenCommand
/// Implements token rotation pattern for enhanced security
///
/// Dependencies:
/// - IRefreshTokenRepository: Access RefreshTokens and commit
/// - IOperatorRepository: Fetch operator data (polymorphic design)
/// - ITokenService: Generate new tokens
/// - JwtOptions: Get token expiry configuration
///
/// Token Rotation Pattern:
/// - Old token → Revoke (set RevokedAt, ReplacedByToken)
/// - New token → Generate and store
/// - Access token → Always generate fresh
/// - Creates audit chain for security
///
/// Polymorphic Design:
/// - RefreshToken no longer has Operator navigation property
/// - Must fetch operator separately using PrincipalId
/// - Supports future extensibility (Employee, Customer, etc.)
///
/// Security Features:
/// - Detects token reuse (revoked token used again)
/// - Limits token lifetime
/// - Operator status revalidation
/// - IP and UserAgent tracking
///
/// Performance:
/// - Two queries: token + operator (can't use Include with polymorphic)
/// - Fast token generation (~1-3ms)
/// - Single transaction commit
///
/// Error Handling:
/// - Invalid/expired/revoked token → 401 Unauthorized
/// - Operator not found → 401 Unauthorized
/// - Operator not active → 403 Forbidden
/// - Database errors → Propagated
/// </summary>
public sealed class RefreshTokenCommandHandler
    : IRequestHandler<RefreshTokenCommand, Result<LoginResponse>>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IOperatorRepository _operatorRepository;
    private readonly ITokenService _tokenService;
    private readonly JwtOptions _jwtOptions;

    public RefreshTokenCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        IOperatorRepository operatorRepository,
        ITokenService tokenService,
        IOptions<JwtOptions> jwtOptions)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _operatorRepository = operatorRepository;
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

        // 3. Verify token is for Operator (polymorphic design)
        if (existingToken.UserType != UserType.Operator)
        {
            return Result.Failure<LoginResponse>(
                AuthenticationErrors.InvalidRefreshToken());
        }

        // 4. Fetch operator separately (no navigation property in polymorphic design)
        var @operator = await _operatorRepository.GetByIdAsync(
            existingToken.PrincipalId,
            cancellationToken);

        if (@operator is null)
        {
            return Result.Failure<LoginResponse>(
                AuthenticationErrors.InvalidRefreshToken());
        }

        // 5. Validate operator status

        if (@operator.Status != OperatorStatus.Active)
        {
            if (@operator.Status == OperatorStatus.Suspended)
            {
                return Result.Failure<LoginResponse>(
                    AuthenticationErrors.AccountSuspended());
            }

            return Result.Failure<LoginResponse>(
                AuthenticationErrors.AccountNotActive());
        }

        // 6. Check if account is locked
        if (@operator.LockedUntilUtc.HasValue && @operator.LockedUntilUtc.Value > DateTime.UtcNow)
        {
            return Result.Failure<LoginResponse>(
                AuthenticationErrors.AccountLockedOut(@operator.LockedUntilUtc));
        }

        // 7. Generate new access token
        var accessTokenResult = _tokenService.GenerateAccessToken(@operator);

        // 8. Generate new refresh token (inherit same expiry duration)
        var originalExpiryDuration = existingToken.ExpiresAt - existingToken.CreatedAtUtc;
        var newRefreshTokenExpiry = DateTime.UtcNow.Add(originalExpiryDuration);
        var newRefreshToken = _tokenService.GenerateRefreshToken(newRefreshTokenExpiry);

        // 9. Revoke old token (token rotation)
        existingToken.Revoke(request.IpAddress, newRefreshToken);

        // 10. Store new refresh token with polymorphic design
        var newRefreshTokenEntity = Domain.Entities.RefreshToken.Create(
            UserType.Operator,      // Polymorphic design: specify user type
            @operator.Id,           // Operator ID becomes PrincipalId
            newRefreshToken,
            newRefreshTokenExpiry,
            request.IpAddress,
            request.UserAgent
        );

        _refreshTokenRepository.Add(newRefreshTokenEntity);
        // UnitOfWorkBehavior will commit

        // 11. Build and return response
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

        return Result.Success(response);
    }
}
