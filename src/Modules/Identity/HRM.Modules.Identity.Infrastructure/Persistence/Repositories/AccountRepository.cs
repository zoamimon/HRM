using HRM.Modules.Identity.Domain.Entities;
using HRM.Modules.Identity.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HRM.Modules.Identity.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for Account aggregate.
/// Provides data access methods using EF Core.
///
/// Table: Identity.Accounts
/// Soft Delete: Global query filter (IsDeleted = false)
/// </summary>
internal sealed class AccountRepository : IAccountRepository
{
    private readonly IdentityDbContext _context;

    public AccountRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<Account?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _context.Accounts
            .FirstOrDefaultAsync(
                a => a.Username.ToLower() == username.ToLower(),
                cancellationToken);
    }

    public async Task<Account?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.Accounts
            .FirstOrDefaultAsync(
                a => a.Email.ToLower() == email.ToLower(),
                cancellationToken);
    }

    public async Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _context.Accounts
            .AsNoTracking()
            .AnyAsync(
                a => a.Username.ToLower() == username.ToLower(),
                cancellationToken);
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.Accounts
            .AsNoTracking()
            .AnyAsync(
                a => a.Email.ToLower() == email.ToLower(),
                cancellationToken);
    }

    public void Add(Account account)
    {
        _context.Accounts.Add(account);
    }

    public void Update(Account account)
    {
        _context.Accounts.Update(account);
    }
}
