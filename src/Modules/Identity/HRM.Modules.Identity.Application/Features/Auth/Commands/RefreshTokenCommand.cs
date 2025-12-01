using HRM.Modules.Identity.Application.DAL;
using HRM.Modules.Identity.Application.Services;
using HRM.Shared.Kernel.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Modules.Identity.Application.Features.Auth.Commands
{
    public class RefreshTokenCommand : IRequest<AuthResponse>
    {
        public string RefreshToken { get; set; }
    }

    public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResponse>
    {
        private readonly IIdentityDbContext _context;
        private readonly ITokenService _tokenService;

        public RefreshTokenCommandHandler(IIdentityDbContext context, ITokenService tokenService)
        {
            _context = context;
            _tokenService = tokenService;
        }

        public async Task<AuthResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
        {
            var userRefreshToken = await _context.UserRefreshTokens
                .SingleOrDefaultAsync(rt => rt.Token == request.RefreshToken, cancellationToken);

            if (userRefreshToken == null || userRefreshToken.IsExpired)
            {
                throw new ValidationException(new List<string> { "Invalid or expired refresh token." });
            }

            var user = await _context.Users
                .Include(u => u.Roles)
                .SingleOrDefaultAsync(u => u.UserId == userRefreshToken.UserId, cancellationToken);

            if (user == null)
            {
                throw new NotFoundException("User not found for the provided refresh token.");
            }

            var newAccessToken = _tokenService.GenerateAccessToken(user, user.Roles);
            var newRefreshTokenString = _tokenService.GenerateRefreshToken();

            userRefreshToken.Update(newRefreshTokenString, DateTime.UtcNow.AddDays(7));

            await _context.SaveChangesAsync(cancellationToken);

            return new AuthResponse
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshTokenString
            };
        }
    }
}
