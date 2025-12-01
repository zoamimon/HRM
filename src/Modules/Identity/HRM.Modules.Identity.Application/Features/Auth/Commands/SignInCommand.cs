using MediatR;

namespace HRM.Modules.Identity.Application.Features.Auth.Commands
{
    public class SignInCommand : IRequest<AuthResponse>
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class AuthResponse
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
    }
}
