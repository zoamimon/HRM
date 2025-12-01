using HRM.Modules.Identity.Application.DAL;
using HRM.Modules.Identity.Application.Services;
using HRM.Modules.Identity.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Modules.Identity.Application.Features.Users.EventHandlers
{
    public class CreateUserForEmployeeCommandHandler : IRequestHandler<CreateUserForEmployeeCommand>
    {
        private readonly IIdentityDbContext _context;
        private readonly IPasswordHasher _passwordHasher;

        public CreateUserForEmployeeCommandHandler(IIdentityDbContext context, IPasswordHasher passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        public async Task Handle(CreateUserForEmployeeCommand request, CancellationToken cancellationToken)
        {
            var emailExists = await _context.Users.AnyAsync(u => u.Email == request.Email, cancellationToken);
            if (emailExists)
            {
                // User might already exist, handle accordingly (e.g., log a warning)
                return;
            }

            // For simplicity, generate a random password. In a real scenario, you'd have a more robust process.
            var password = GenerateRandomPassword();
            var hashedPassword = _passwordHasher.HashPassword(password);

            var user = new User(request.EmployeeId, request.Email, hashedPassword);

            const string defaultRoleName = "Employee";
            var defaultRole = await _context.Roles.SingleOrDefaultAsync(r => r.Name == defaultRoleName, cancellationToken);

            if (defaultRole != null)
            {
                user.AddRole(defaultRole);
            }

            await _context.Users.AddAsync(user, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        private string GenerateRandomPassword(int length = 12)
        {
            const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*()";
            var chars = new char[length];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                byte[] data = new byte[length];
                rng.GetBytes(data);
                for (int i = 0; i < length; i++)
                {
                    chars[i] = validChars[data[i] % validChars.Length];
                }
            }
            return new string(chars);
        }
    }
}
