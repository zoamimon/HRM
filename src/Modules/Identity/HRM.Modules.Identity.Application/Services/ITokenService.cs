using HRM.Modules.Identity.Domain.Entities;

namespace HRM.Modules.Identity.Application.Services
{
    public interface ITokenService
    {
        string GenerateAccessToken(User user, IEnumerable<Role> roles);
        string GenerateRefreshToken();
    }
}
