using HRM.Modules.Identity.Domain.Entities;

namespace HRM.Modules.Identity.Domain.Repositories;

/// <summary>
/// Repository interface for Operator aggregate
/// Defines data access contract - implemented in Infrastructure layer
///
/// Design Patterns:
/// - Repository Pattern: Abstracts data access from domain
/// - Aggregate Root: Only repository for Operator aggregate
/// - Unit of Work: Add/Update/Remove don't save immediately (use DbContext.SaveChangesAsync)
///
/// Implementation Notes:
/// - All async methods for scalability
/// - CancellationToken support for long-running queries
/// - No IQueryable exposure (keeps domain pure)
/// - Returns null for not found (use Result pattern in Application layer)
///
/// Query Methods:
/// - GetByIdAsync: Retrieve by primary key (for ActivateOperator command)
/// - GetByUsernameAsync: Retrieve by username (for Login command)
/// - GetByEmailAsync: Retrieve by email (for password reset)
/// - ExistsByUsernameAsync: Check uniqueness (for RegisterOperator validation)
/// - ExistsByEmailAsync: Check uniqueness (for RegisterOperator validation)
///
/// Command Methods:
/// - Add: Register new operator (INSERT)
/// - Update: Modify existing operator (UPDATE) - EF tracks changes automatically
/// - Remove: Delete operator (soft delete via Entity.Delete())
///
/// Usage Example (Application Layer):
/// <code>
/// // In RegisterOperatorCommandHandler
/// if (await _operatorRepository.ExistsByUsernameAsync(command.Username, cancellationToken))
///     return Result.Failure<Guid>(OperatorErrors.UsernameAlreadyExists(command.Username));
///
/// var @operator = Operator.Register(command.Username, command.Email, passwordHash, command.FullName);
/// _operatorRepository.Add(@operator);
/// await _unitOfWork.SaveChangesAsync(cancellationToken); // Commit transaction
///
/// return Result.Success(@operator.Id);
/// </code>
/// </summary>
public interface IOperatorRepository
{
    /// <summary>
    /// Get operator by ID
    /// Returns null if not found
    ///
    /// Use Cases:
    /// - ActivateOperator command
    /// - GetOperator query
    /// - UpdateOperator command
    /// </summary>
    /// <param name="id">Operator ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Operator entity or null if not found</returns>
    Task<Operator?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get operator by username
    /// Returns null if not found
    ///
    /// Use Cases:
    /// - Login command (authenticate with username/password)
    /// - Check if username exists before registration
    ///
    /// Performance:
    /// - Indexed column (see 002_CreateIndexes.sql)
    /// - Case-insensitive comparison (SQL: COLLATE SQL_Latin1_General_CP1_CI_AS)
    /// </summary>
    /// <param name="username">Username (case-insensitive)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Operator entity or null if not found</returns>
    Task<Operator?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get operator by email
    /// Returns null if not found
    ///
    /// Use Cases:
    /// - Forgot password (send reset link to email)
    /// - Check if email exists before registration
    ///
    /// Performance:
    /// - Indexed column (see 002_CreateIndexes.sql)
    /// - Case-insensitive comparison
    /// </summary>
    /// <param name="email">Email address (case-insensitive)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Operator entity or null if not found</returns>
    Task<Operator?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if username already exists
    /// More efficient than GetByUsernameAsync when you only need existence check
    ///
    /// Use Cases:
    /// - RegisterOperatorCommandValidator (FluentValidation)
    /// - Username availability check
    ///
    /// Performance:
    /// - Uses EXISTS query (stops at first match)
    /// - Returns early without loading full entity
    /// </summary>
    /// <param name="username">Username to check (case-insensitive)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if username exists, false otherwise</returns>
    Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if email already exists
    /// More efficient than GetByEmailAsync when you only need existence check
    ///
    /// Use Cases:
    /// - RegisterOperatorCommandValidator (FluentValidation)
    /// - Email availability check
    ///
    /// Performance:
    /// - Uses EXISTS query (stops at first match)
    /// - Returns early without loading full entity
    /// </summary>
    /// <param name="email">Email to check (case-insensitive)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if email exists, false otherwise</returns>
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add new operator to repository
    /// Does NOT save to database immediately (use UnitOfWork.SaveChangesAsync)
    ///
    /// Use Cases:
    /// - RegisterOperator command
    ///
    /// Usage:
    /// <code>
    /// var @operator = Operator.Register(...);
    /// _operatorRepository.Add(@operator);
    /// await _unitOfWork.SaveChangesAsync(cancellationToken);
    /// </code>
    /// </summary>
    /// <param name="operator">Operator entity to add</param>
    void Add(Operator @operator);

    /// <summary>
    /// Update existing operator
    /// Does NOT save to database immediately (use UnitOfWork.SaveChangesAsync)
    ///
    /// Note: With EF Core change tracking, explicit Update() call is usually not needed
    /// Just modify the entity properties and SaveChangesAsync will detect changes
    ///
    /// Use Cases:
    /// - ActivateOperator command
    /// - UpdateOperatorProfile command
    /// - ChangePassword command
    ///
    /// Usage:
    /// <code>
    /// var @operator = await _operatorRepository.GetByIdAsync(id, cancellationToken);
    /// @operator.Activate();
    /// // No need to call Update() - EF tracks changes
    /// await _unitOfWork.SaveChangesAsync(cancellationToken);
    /// </code>
    /// </summary>
    /// <param name="operator">Operator entity to update</param>
    void Update(Operator @operator);

    /// <summary>
    /// Remove operator from repository (soft delete)
    /// Does NOT save to database immediately (use UnitOfWork.SaveChangesAsync)
    ///
    /// Note: This performs SOFT DELETE (sets IsDeleted = true, DeletedAtUtc = now)
    /// Actual implementation should call @operator.Delete() before removing
    ///
    /// Use Cases:
    /// - DeactivateOperator command
    /// - DeleteOperator command (admin only)
    ///
    /// Usage:
    /// <code>
    /// var @operator = await _operatorRepository.GetByIdAsync(id, cancellationToken);
    /// @operator.Delete(); // Soft delete
    /// // No need to call Remove() - just let SaveChangesAsync persist the change
    /// await _unitOfWork.SaveChangesAsync(cancellationToken);
    /// </code>
    /// </summary>
    /// <param name="operator">Operator entity to remove</param>
    void Remove(Operator @operator);
}
