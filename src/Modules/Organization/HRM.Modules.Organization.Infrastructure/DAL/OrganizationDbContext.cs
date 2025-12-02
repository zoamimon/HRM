using HRM.Modules.Organization.Application.DAL;
using HRM.Modules.Organization.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HRM.Modules.Organization.Infrastructure.DAL
{
    public class OrganizationDbContext : DbContext, IOrganizationDbContext
    {
        public DbSet<Company> Companies { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Position> Positions { get; set; }

        public OrganizationDbContext(DbContextOptions<OrganizationDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Company>(builder =>
            {
                builder.HasKey(c => c.CompanyId);
                builder.Property(c => c.Name).IsRequired().HasMaxLength(200);

                builder.HasOne(c => c.Parent)
                       .WithMany(c => c.Children)
                       .HasForeignKey(c => c.ParentId)
                       .OnDelete(DeleteBehavior.Restrict);

                builder.HasMany(c => c.Departments)
                       .WithOne(d => d.Company)
                       .HasForeignKey(d => d.CompanyId);
            });

            modelBuilder.Entity<Department>(builder =>
            {
                builder.HasKey(d => d.DepartmentId);
                builder.Property(d => d.Name).IsRequired().HasMaxLength(200);

                builder.HasOne(d => d.Parent)
                       .WithMany(d => d.Children)
                       .HasForeignKey(d => d.ParentId)
                       .OnDelete(DeleteBehavior.Restrict);

                builder.HasMany(d => d.Positions)
                       .WithOne(p => p.Department)
                       .HasForeignKey(p => p.DepartmentId);
            });

            modelBuilder.Entity<Position>(builder =>
            {
                builder.HasKey(p => p.PositionId);
                builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
            });
        }
    }
}
