using HRM.Modules.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HRM.Modules.Identity.Application.Abstractions.Data;

/// <summary>
/// Query context interface for Identity module
/// Provides read-only access to DbSets for query handlers
///
/// Purpose:
/// - Dependency Inversion: Application depends on abstraction, not Infrastructure
/// - Query handlers in Application layer can access data without referencing EF Core implementation
/// - Keeps Application layer independent of Infrastructure
///
/// Implementation:
/// - IdentityDbContext implements this interface
/// - Registered in DI as scoped service
///
/// Usage:
/// <code>
/// public class GetOperatorsQueryHandler : IQueryHandler<...>
/// {
///     private readonly IIdentityQueryContext _context;
///
///     public async Task<PagedResult<OperatorSummaryDto>> Handle(...)
///     {
///         var query = _context.Operators.AsNoTracking();
///         // ... query logic
///     }
/// }
/// </code>
/// </summary>
public interface IIdentityQueryContext
{
    /// <summary>
    /// Operators table (read-only access)
    /// </summary>
    DbSet<Operator> Operators { get; }

    /// <summary>
    /// Refresh tokens table (read-only access)
    /// </summary>
    DbSet<RefreshToken> RefreshTokens { get; }
}
