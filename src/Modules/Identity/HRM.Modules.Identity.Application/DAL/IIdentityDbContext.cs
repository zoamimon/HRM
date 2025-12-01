using HRM.Modules.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HRM.Modules.Identity.Application.DAL
{
    public interface IIdentityDbContext
    {
        DbSet<User> Users { get; }
        DbSet<Role> Roles { get; }
        DbSet<Permission> Permissions { get; }
        DbSet<UserRefreshToken> UserRefreshTokens { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
