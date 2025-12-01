using HRM.Shared.Kernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace HRM.Shared.Kernel.Interfaces
{
    public interface IModuleDbContext
    {
        DbSet<OutboxMessage> OutboxMessages { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
