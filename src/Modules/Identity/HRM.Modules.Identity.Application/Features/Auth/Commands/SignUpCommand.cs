using MediatR;

namespace HRM.Modules.Identity.Application.Features.Auth.Commands
{
    public class SignUpCommand : IRequest<Guid>
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }
}
