using HRM.Modules.Organization.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HRM.Modules.Organization.Application.DAL
{
    public interface IOrganizationDbContext
    {
        DbSet<Company> Companies { get; }
        DbSet<Department> Departments { get; }
        DbSet<Position> Positions { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
