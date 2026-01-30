using HRM.Modules.Identity.Domain.Entities;

namespace HRM.Modules.Identity.Domain.Repositories;

/// <summary>
/// Repository interface for Account aggregate.
/// Account is the unified authentication entity (replaces Operator for login).
///
/// Query Methods:
/// - GetByIdAsync: Retrieve by primary key
/// - GetByUsernameAsync: Retrieve by username (for login)
/// - GetByEmailAsync: Retrieve by email (for login/password reset)
/// - ExistsByUsernameAsync: Check uniqueness (for registration)
/// - ExistsByEmailAsync: Check uniqueness (for registration)
///
/// Command Methods:
/// - Add: Register new account (INSERT)
/// - Update: Modify existing account (UPDATE)
/// </summary>
public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Account?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    Task<Account?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken = default);

    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);

    void Add(Account account);

    void Update(Account account);
}
