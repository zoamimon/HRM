namespace HRM.BuildingBlocks.Domain.Abstractions.SoftDelete;

/// <summary>
/// Marker interface for entities that support soft delete
/// Soft delete marks records as deleted without physically removing them from database
///
/// Purpose:
/// - Data Recovery: Ability to restore accidentally deleted records
/// - Audit Trail: Keep history of deleted records for compliance
/// - Referential Integrity: Maintain foreign key relationships
/// - Business Rules: Some records cannot be hard-deleted (e.g., employees with payroll history)
///
/// Soft Delete vs Hard Delete:
///
/// Hard Delete (Physical):
/// - Permanently removes record from database (DELETE FROM ...)
/// - Cannot be recovered
/// - Breaks foreign key relationships
/// - Use for: Truly temporary data, test data, GDPR right to be forgotten
///
/// Soft Delete (Logical):
/// - Marks record as deleted (UPDATE ... SET IsDeleted = 1)
/// - Can be restored
/// - Maintains referential integrity
/// - Use for: Business data, audit requirements, recoverable deletions
///
/// Implementation Pattern:
/// <code>
/// // Entity implements ISoftDeletable
/// public class Employee : Entity, IAggregateRoot, ISoftDeletable
/// {
///     public bool IsDeleted { get; private set; }
///     public DateTime? DeletedAtUtc { get; private set; }
///
///     public void Delete()
///     {
///         if (IsDeleted)
///             throw new InvalidOperationException("Employee already deleted");
///
///         IsDeleted = true;
///         DeletedAtUtc = DateTime.UtcNow;
///
///         // Raise domain event
///         AddDomainEvent(new EmployeeDeletedDomainEvent(Id));
///     }
///
///     public void Restore()
///     {
///         if (!IsDeleted)
///             throw new InvalidOperationException("Employee is not deleted");
///
///         IsDeleted = false;
///         DeletedAtUtc = null;
///
///         // Raise domain event
///         AddDomainEvent(new EmployeeRestoredDomainEvent(Id));
///     }
/// }
/// </code>
///
/// Global Query Filter (EF Core):
/// Automatically exclude soft-deleted records from all queries:
/// <code>
/// // In ModuleDbContext.OnModelCreating
/// foreach (var entityType in modelBuilder.Model.GetEntityTypes())
/// {
///     if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
///     {
///         var parameter = Expression.Parameter(entityType.ClrType, "e");
///         var property = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
///         var filter = Expression.Lambda(Expression.Equal(property, Expression.Constant(false)), parameter);
///
///         entityType.SetQueryFilter(filter);
///     }
/// }
/// </code>
///
/// Query Behavior:
/// <code>
/// // Normal query - automatically excludes soft-deleted
/// var employees = await _context.Employees.ToListAsync();
/// // SQL: SELECT * FROM Employees WHERE IsDeleted = 0
///
/// // Include soft-deleted explicitly
/// var allEmployees = await _context.Employees
///     .IgnoreQueryFilters()
///     .ToListAsync();
/// // SQL: SELECT * FROM Employees
///
/// // Get only soft-deleted
/// var deletedEmployees = await _context.Employees
///     .IgnoreQueryFilters()
///     .Where(e => e.IsDeleted)
///     .ToListAsync();
/// // SQL: SELECT * FROM Employees WHERE IsDeleted = 1
/// </code>
///
/// Command Pattern:
/// <code>
/// // Delete command
/// public sealed record DeleteEmployeeCommand(Guid EmployeeId) : ICommand;
///
/// public class DeleteEmployeeCommandHandler
/// {
///     public async Task&lt;Result&gt; Handle(DeleteEmployeeCommand cmd, ...)
///     {
///         var employee = await _repository.GetByIdAsync(cmd.EmployeeId);
///         if (employee is null)
///             return Result.Failure(Error.NotFound(...));
///
///         // Soft delete (no data lost)
///         employee.Delete();
///
///         // Domain event dispatched in UnitOfWork.CommitAsync
///         return Result.Success();
///     }
/// }
///
/// // Restore command
/// public sealed record RestoreEmployeeCommand(Guid EmployeeId) : ICommand;
///
/// public class RestoreEmployeeCommandHandler
/// {
///     public async Task&lt;Result&gt; Handle(RestoreEmployeeCommand cmd, ...)
///     {
///         // Must include soft-deleted to find it
///         var employee = await _context.Employees
///             .IgnoreQueryFilters()
///             .FirstOrDefaultAsync(e => e.Id == cmd.EmployeeId);
///
///         if (employee is null)
///             return Result.Failure(Error.NotFound(...));
///
///         if (!employee.IsDeleted)
///             return Result.Failure(Error.Conflict("Employee.NotDeleted", "Employee is not deleted"));
///
///         employee.Restore();
///         return Result.Success();
///     }
/// }
/// </code>
///
/// UI Considerations:
/// - Show "Deleted" badge/indicator for soft-deleted items (when IgnoreQueryFilters used)
/// - Provide "Restore" button for admins
/// - Show DeletedAtUtc timestamp
/// - Consider "Recycle Bin" view showing all soft-deleted items
///
/// Database Indexes:
/// <code>
/// // Index on IsDeleted for efficient filtering
/// CREATE INDEX IX_Employees_IsDeleted ON Employees(IsDeleted)
///     WHERE IsDeleted = 0
///
/// // Or filtered index (SQL Server)
/// CREATE INDEX IX_Employees_Active ON Employees(LastName, FirstName)
///     WHERE IsDeleted = 0
/// </code>
///
/// Compliance:
/// - GDPR "Right to be Forgotten": May require hard delete after soft delete period
/// - Implement cleanup job to permanently delete old soft-deleted records:
/// <code>
/// public class CleanupOldDeletedRecordsJob
/// {
///     public async Task ExecuteAsync()
///     {
///         var cutoffDate = DateTime.UtcNow.AddDays(-90); // 90 day retention
///
///         var oldDeleted = await _context.Employees
///             .IgnoreQueryFilters()
///             .Where(e => e.IsDeleted && e.DeletedAtUtc < cutoffDate)
///             .ToListAsync();
///
///         _context.Employees.RemoveRange(oldDeleted); // Hard delete
///         await _context.SaveChangesAsync();
///     }
/// }
/// </code>
///
/// Performance Impact:
/// - Minimal: WHERE IsDeleted = 0 added to all queries (indexed)
/// - Soft delete is UPDATE, not DELETE (slightly slower)
/// - Database size grows over time (mitigate with cleanup job)
/// - Indexes may be larger (include deleted records)
/// </summary>
public interface ISoftDeletable
{
    /// <summary>
    /// Indicates whether the entity is soft-deleted
    /// True = deleted (hidden from queries)
    /// False = active (normal queries)
    ///
    /// Database:
    /// - Type: BIT (SQL Server), BOOLEAN (PostgreSQL)
    /// - Default: False (0)
    /// - Index: Yes (for efficient filtering)
    /// </summary>
    bool IsDeleted { get; }

    /// <summary>
    /// Timestamp when entity was soft-deleted
    /// NULL = not deleted (active)
    /// DateTime = deleted timestamp
    ///
    /// Uses:
    /// - Audit: When was this deleted?
    /// - Cleanup: Delete records older than X days
    /// - Restore: Show user when they deleted it
    /// - Sorting: Order deleted items by deletion time
    ///
    /// Database:
    /// - Type: DATETIME2 (SQL Server), TIMESTAMP (PostgreSQL)
    /// - Nullable: Yes
    /// - UTC: Yes (always store in UTC)
    /// </summary>
    DateTime? DeletedAtUtc { get; }

    /// <summary>
    /// Performs soft delete operation
    /// Sets IsDeleted = true and DeletedAtUtc = now
    ///
    /// Should:
    /// - Validate entity is not already deleted
    /// - Set IsDeleted to true
    /// - Set DeletedAtUtc to DateTime.UtcNow
    /// - Raise domain event (e.g., EmployeeDeletedDomainEvent)
    /// - NOT call database directly (let UnitOfWork handle it)
    ///
    /// Throws:
    /// - InvalidOperationException if already deleted
    /// - DomainException if deletion not allowed (business rule)
    /// </summary>
    void Delete();

    /// <summary>
    /// Restores a soft-deleted entity
    /// Sets IsDeleted = false and DeletedAtUtc = null
    ///
    /// Should:
    /// - Validate entity is currently deleted
    /// - Set IsDeleted to false
    /// - Set DeletedAtUtc to null
    /// - Raise domain event (e.g., EmployeeRestoredDomainEvent)
    /// - NOT call database directly (let UnitOfWork handle it)
    ///
    /// Throws:
    /// - InvalidOperationException if not deleted
    /// - DomainException if restoration not allowed
    /// </summary>
    void Restore();
}
