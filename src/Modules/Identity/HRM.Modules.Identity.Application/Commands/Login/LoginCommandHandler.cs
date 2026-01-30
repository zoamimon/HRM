using HRM.BuildingBlocks.Domain.Abstractions.Results;
using HRM.Modules.Identity.Domain.Enums;
using HRM.Modules.Identity.Application.Abstractions.Authentication;
using HRM.Modules.Identity.Application.Configuration;
using HRM.Modules.Identity.Application.Errors;
using HRM.Modules.Identity.Domain.Entities;
using HRM.Modules.Identity.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Options;

namespace HRM.Modules.Identity.Application.Commands.Login;

/// <summary>
/// Handler for LoginCommand.
/// Authenticates via Account entity (unified login) and generates JWT access + refresh tokens.
///
/// Dependencies:
/// - IAccountRepository: Find account by username/email
/// - IPasswordHasher: Verify password against stored hash
/// - ITokenService: Generate JWT and refresh tokens
/// - JwtOptions: Get token expiry configuration
/// - IRefreshTokenRepository: Save refresh token (committed by UnitOfWorkBehavior)
///
/// Security Features:
/// - Constant-time password comparison (via BCrypt)
/// - Account lockout after failed attempts
/// - Generic error messages (prevent enumeration)
/// - IP and UserAgent tracking
/// - Remember Me support (7 vs 30 days)
/// </summary>
public sealed class LoginCommandHandler
    : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly IAccountRepository _accountRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly JwtOptions _jwtOptions;
    private readonly IRefreshTokenRepository _refreshTokenRepository;

    public LoginCommandHandler(
        IAccountRepository accountRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IOptions<JwtOptions> jwtOptions,
        IRefreshTokenRepository refreshTokenRepository)
    {
        _accountRepository = accountRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _jwtOptions = jwtOptions.Value;
        _refreshTokenRepository = refreshTokenRepository;
    }

    public async Task<Result<LoginResponse>> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Find account by username or email
        var account = await FindAccountAsync(request.UsernameOrEmail, cancellationToken);

        if (account is null)
        {
            return Result.Failure<LoginResponse>(
                AuthenticationErrors.InvalidCredentials());
        }

        // 2. Try auto-unlock expired lockouts
        account.TryUnlock();

        // 3. Check if account is locked
        if (account.IsLocked())
        {
            return Result.Failure<LoginResponse>(
                AuthenticationErrors.AccountLockedOut(account.LockedUntilUtc));
        }

        // 4. Check account status
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

        // 5. Verify password
        bool isPasswordValid = _passwordHasher.VerifyPassword(
            request.Password,
            account.PasswordHash);

        if (!isPasswordValid)
        {
            account.RecordFailedLogin();
            _accountRepository.Update(account);

            if (account.IsLocked())
            {
                return Result.Failure<LoginResponse>(
                    AuthenticationErrors.AccountLockedOut(account.LockedUntilUtc));
            }

            return Result.Failure<LoginResponse>(
                AuthenticationErrors.InvalidCredentials());
        }

        // 6. Password correct — reset failed attempts, record login
        account.RecordLogin();
        _accountRepository.Update(account);

        // 7. Generate access token (JWT)
        var accessTokenResult = _tokenService.GenerateAccessToken(account);

        // 8. Generate refresh token with Remember Me support
        var refreshExpiryDays = request.RememberMe
            ? _jwtOptions.RememberMeExpiryDays
            : _jwtOptions.RefreshTokenExpiryDays;

        var refreshTokenExpiry = DateTime.UtcNow.AddDays(refreshExpiryDays);
        var refreshToken = _tokenService.GenerateRefreshToken(refreshTokenExpiry);

        // 9. Store refresh token
        var refreshTokenEntity = RefreshToken.Create(
            account.AccountType,
            account.Id,
            refreshToken,
            refreshTokenExpiry,
            request.IpAddress,
            request.UserAgent
        );

        _refreshTokenRepository.Add(refreshTokenEntity);
        // UnitOfWorkBehavior will commit

        // 10. Build and return response
        var response = new LoginResponse
        {
            AccessToken = accessTokenResult.Token,
            RefreshToken = refreshToken,
            AccessTokenExpiry = accessTokenResult.ExpiresAt,
            RefreshTokenExpiry = refreshTokenExpiry,
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

    /// <summary>
    /// Find account by username or email (flexible login).
    /// </summary>
    private async Task<Account?> FindAccountAsync(
        string usernameOrEmail,
        CancellationToken cancellationToken)
    {
        var account = await _accountRepository.GetByUsernameAsync(
            usernameOrEmail, cancellationToken);

        if (account is not null)
            return account;

        return await _accountRepository.GetByEmailAsync(
            usernameOrEmail, cancellationToken);
    }
}
