using HRM.Modules.Identity.Application.DAL;
using HRM.Modules.Identity.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Modules.Identity.Application.Features.Auth.Commands
{
    public class SignInCommandHandler : IRequestHandler<SignInCommand, AuthResponse>
    {
        private readonly IIdentityDbContext _context;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ITokenService _tokenService;

        public SignInCommandHandler(IIdentityDbContext context, IPasswordHasher passwordHasher, ITokenService tokenService)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
        }

        public async Task<AuthResponse> Handle(SignInCommand request, CancellationToken cancellationToken)
        {
            var user = await _context.Users
                .Include(u => u.Roles)
                .SingleOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

            if (user == null || !_passwordHasher.VerifyPassword(user.HashedPassword, request.Password))
            {
                throw new Exception("Invalid credentials."); // Replace with custom exception
            }

            var accessToken = _tokenService.GenerateAccessToken(user, user.Roles);
            var refreshTokenString = _tokenService.GenerateRefreshToken();

            var refreshToken = new Domain.Entities.UserRefreshToken(
                user.UserId,
                refreshTokenString,
                DateTime.UtcNow.AddDays(7));

            await _context.UserRefreshTokens.AddAsync(refreshToken, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshTokenString
            };
        }
    }
}
