using HRM.Modules.Identity.Application.Services;
using BCryptNet = BCrypt.Net.BCrypt;

namespace HRM.Modules.Identity.Infrastructure.Services
{
    public class BCryptPasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password)
        {
            return BCryptNet.HashPassword(password);
        }

        public bool VerifyPassword(string hashedPassword, string providedPassword)
        {
            return BCryptNet.Verify(providedPassword, hashedPassword);
        }
    }
}
