using HRM.Modules.Identity.Application.DAL;
using HRM.Modules.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HRM.Modules.Identity.Infrastructure.DAL
{
    public class IdentityDbContext : DbContext, IIdentityDbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<UserRefreshToken> UserRefreshTokens { get; set; }

        public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(builder =>
            {
                builder.HasKey(u => u.UserId);
                builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
                builder.HasIndex(u => u.Email).IsUnique();
                builder.Property(u => u.HashedPassword).IsRequired();

                builder.HasMany(u => u.Roles)
                       .WithMany()
                       .UsingEntity("UserRoles");

                builder.HasMany(u => u.RefreshTokens)
                    .WithOne()
                    .HasForeignKey(rt => rt.UserId);
            });

            modelBuilder.Entity<Role>(builder =>
            {
                builder.HasKey(r => r.RoleId);
                builder.Property(r => r.Name).IsRequired().HasMaxLength(100);

                builder.HasMany(r => r.Permissions)
                       .WithMany()
                       .UsingEntity("RolePermissions");
            });

            modelBuilder.Entity<Permission>(builder =>
            {
                builder.HasKey(p => p.PermissionId);
                builder.Property(p => p.Name).IsRequired().HasMaxLength(100);
            });

            // Seed initial data
            SeedData(modelBuilder);
        }

        private void SeedData(ModelBuilder modelBuilder)
        {
            // Seed Permissions
            var permissions = new[]
            {
                new Permission(1, Permission.Permissions.Read),
                new Permission(2, Permission.Permissions.Create),
                new Permission(3, Permission.Permissions.Update),
                new Permission(4, Permission.Permissions.Delete)
            };
            modelBuilder.Entity<Permission>().HasData(permissions);

            // Seed Roles
            var roles = new[]
            {
                new Role(Guid.NewGuid(), "Admin"),
                new Role(Guid.NewGuid(), "Employee")
            };
            modelBuilder.Entity<Role>().HasData(roles);
        }
    }
}
