using HRM.BuildingBlocks.Domain.Abstractions.Results;
using HRM.BuildingBlocks.Domain.Enums;
using HRM.Modules.Identity.Application.Abstractions.Authentication;
using HRM.Modules.Identity.Application.Configuration;
using HRM.Modules.Identity.Application.Errors;
using HRM.Modules.Identity.Domain.Entities;
using HRM.Modules.Identity.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Options;

namespace HRM.Modules.Identity.Application.Commands.Login;

/// <summary>
/// Handler for LoginCommand
/// Authenticates operator and generates JWT access + refresh tokens
///
/// Dependencies:
/// - IOperatorRepository: Find operator by username/email
/// - IPasswordHasher: Verify password against stored hash
/// - ITokenService: Generate JWT and refresh tokens
/// - JwtOptions: Get token expiry configuration
/// - IRefreshTokenRepository: Save refresh token (committed by UnitOfWorkBehavior)
///
/// Security Features:
/// - Constant-time password comparison (via BCrypt)
/// - Account lockout after 5 failed attempts (15 minutes)
/// - Generic error messages (prevent enumeration)
/// - IP and UserAgent tracking
/// - Remember Me support (7 vs 30 days)
///
/// Performance:
/// - Single database query to find operator
/// - Password hashing is slow by design (~100-200ms)
/// - Token generation is fast (~1-2ms)
///
/// Error Handling:
/// - Invalid credentials → 401 Unauthorized
/// - Account locked → 403 Forbidden
/// - Account not active → 403 Forbidden
/// - Other errors → Propagated to caller
/// </summary>
public sealed class LoginCommandHandler
    : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly IOperatorRepository _operatorRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly JwtOptions _jwtOptions;
    private readonly IRefreshTokenRepository _refreshTokenRepository;

    public LoginCommandHandler(
        IOperatorRepository operatorRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IOptions<JwtOptions> jwtOptions,
        IRefreshTokenRepository refreshTokenRepository)
    {
        _operatorRepository = operatorRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _jwtOptions = jwtOptions.Value;
        _refreshTokenRepository = refreshTokenRepository;
    }

    public async Task<Result<LoginResponse>> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Find operator by username or email
        var @operator = await FindOperatorAsync(request.UsernameOrEmail, cancellationToken);

        if (@operator is null)
        {
            // Don't reveal that user doesn't exist (security)
            return Result.Failure<LoginResponse>(
                AuthenticationErrors.InvalidCredentials());
        }

        // 2. Check if account is locked
        if (@operator.IsLocked())
        {
            return Result.Failure<LoginResponse>(
                AuthenticationErrors.AccountLockedOut(@operator.LockedUntilUtc));
        }

        // 3. Check account status
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

        // 4. Verify password
        bool isPasswordValid = _passwordHasher.VerifyPassword(
            request.Password,
            @operator.PasswordHash);

        if (!isPasswordValid)
        {
            // Record failed login attempt and potentially lock account
            @operator.RecordFailedLogin();
            _operatorRepository.Update(@operator);

            // Check if account got locked after this failed attempt
            if (@operator.IsLocked())
            {
                return Result.Failure<LoginResponse>(
                    AuthenticationErrors.AccountLockedOut(@operator.LockedUntilUtc));
            }

            // Generic error (don't reveal password was wrong)
            return Result.Failure<LoginResponse>(
                AuthenticationErrors.InvalidCredentials());
        }

        // 5. Password correct - reset failed attempts and update last login
        @operator.RecordLogin();
        _operatorRepository.Update(@operator);

        // 6. Generate access token (JWT)
        var accessTokenResult = _tokenService.GenerateAccessToken(@operator);

        // 7. Generate refresh token with Remember Me support
        var refreshExpiryDays = request.RememberMe
            ? _jwtOptions.RememberMeExpiryDays
            : _jwtOptions.RefreshTokenExpiryDays;

        var refreshTokenExpiry = DateTime.UtcNow.AddDays(refreshExpiryDays);
        var refreshToken = _tokenService.GenerateRefreshToken(refreshTokenExpiry);

        // 8. Store refresh token in database
        var refreshTokenEntity = Domain.Entities.RefreshToken.Create(
            AccountType.System,     // System account (Operator)
            @operator.Id,           // Operator ID becomes PrincipalId
            refreshToken,
            refreshTokenExpiry,
            request.IpAddress,
            request.UserAgent
        );

        _refreshTokenRepository.Add(refreshTokenEntity);
        // UnitOfWorkBehavior will commit

        // 9. Build and return response
        var response = new LoginResponse
        {
            AccessToken = accessTokenResult.Token,
            RefreshToken = refreshToken,
            AccessTokenExpiry = accessTokenResult.ExpiresAt,
            RefreshTokenExpiry = refreshTokenExpiry,
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

    /// <summary>
    /// Find operator by username or email
    /// Flexible login - user can provide either
    ///
    /// Strategy:
    /// 1. Try username first (most common)
    /// 2. If not found, try email
    /// 3. Return null if neither found
    ///
    /// Performance:
    /// - Indexed columns (fast lookup)
    /// - Max 2 database queries
    /// - Usually just 1 query (username is common)
    /// </summary>
    private async Task<Operator?> FindOperatorAsync(
        string usernameOrEmail,
        CancellationToken cancellationToken)
    {
        // Try username first
        var @operator = await _operatorRepository.GetByUsernameAsync(
            usernameOrEmail,
            cancellationToken);

        if (@operator is not null)
        {
            return @operator;
        }

        // Try email if username not found
        return await _operatorRepository.GetByEmailAsync(
            usernameOrEmail,
            cancellationToken);
    }
}
