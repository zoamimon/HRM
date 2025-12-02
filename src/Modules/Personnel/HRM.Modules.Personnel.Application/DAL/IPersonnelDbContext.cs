using HRM.Modules.Personnel.Domain.Entities;
using HRM.Shared.Kernel.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HRM.Modules.Personnel.Application.DAL
{
    public interface IPersonnelDbContext : IModuleDbContext
    {
        DbSet<Employee> Employees { get; }
    }
}
