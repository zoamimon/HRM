using MediatR;

namespace HRM.BuildingBlocks.Application.Abstractions.Queries;

/// <summary>
/// Interface for query handlers in the CQRS pattern.
/// Handles read operations and returns data directly (no Result wrapper).
/// 
/// Handler Responsibilities:
/// 1. Query database efficiently (use Dapper for complex queries)
/// 2. Apply data scoping for User requests (IDataScopingService)
/// 3. Project to DTOs (don't return domain entities)
/// 4. Handle not found scenarios gracefully (return null or empty)
/// 5. Return data directly to caller
/// 
/// Handler Must NOT:
/// - Modify state (queries are read-only)
/// - Call repositories for write operations
/// - Raise domain events
/// - Use Result wrapper (return data directly)
/// - Return domain entities (use DTOs)
/// 
/// Performance Best Practices:
/// 
/// 1. Use Dapper for Complex Queries:
/// <code>
/// public async Task&lt;List&lt;EmployeeDto&gt;&gt; Handle(...)
/// {
///     var sql = @"
///         SELECT e.Id, e.FirstName, e.LastName, e.Email,
///                d.Name AS DepartmentName,
///                p.Name AS PositionName
///         FROM personnel.Employees e
///         INNER JOIN personnel.EmployeeAssignments ea ON e.Id = ea.EmployeeId
///         INNER JOIN organization.Departments d ON ea.DepartmentId = d.Id
///         INNER JOIN organization.Positions p ON ea.PositionId = p.Id
///         WHERE ea.EndDate IS NULL
///             {0}"; // Data scoping filter injected here
///     
///     var employees = await _connection.QueryAsync&lt;EmployeeDto&gt;(
///         sql,
///         parameters
///     );
///     
///     return employees.ToList();
/// }
/// </code>
/// 
/// 2. Use EF Core with AsNoTracking:
/// <code>
/// public async Task&lt;OperatorDto?&gt; Handle(...)
/// {
///     return await _context.Operators
///         .AsNoTracking() // Critical for read-only queries
///         .Where(o => o.Id == query.OperatorId)
///         .Select(o => new OperatorDto
///         {
///             Id = o.Id,
///             Username = o.Username,
///             Email = o.Email,
///             IsActive = o.IsActive
///         })
///         .FirstOrDefaultAsync(cancellationToken);
/// }
/// </code>
/// 
/// Data Scoping Implementation:
/// <code>
/// public class SearchEmployeesQueryHandler 
///     : IQueryHandler&lt;SearchEmployeesQuery, List&lt;EmployeeDto&gt;&gt;
/// {
///     private readonly IDbConnection _connection;
///     private readonly IDataScopingService _scopingService;
///     
///     public async Task&lt;List&lt;EmployeeDto&gt;&gt; Handle(
///         SearchEmployeesQuery query,
///         CancellationToken cancellationToken)
///     {
///         // Get current user's scope context
///         var scopeContext = await _scopingService.GetCurrentScopeAsync(cancellationToken);
///         
///         // Build base SQL
///         var sql = @"
///             SELECT DISTINCT e.Id, e.FirstName, e.LastName, e.Email
///             FROM personnel.Employees e
///             INNER JOIN personnel.EmployeeAssignments ea ON e.Id = ea.EmployeeId
///             WHERE ea.EndDate IS NULL
///                 {0}"; // Scope filter will be injected
///         
///         var parameters = new DynamicParameters();
///         
///         // Apply data scoping based on user type
///         string scopeFilter = _scopingService.BuildScopeFilter(
///             scopeContext, 
///             parameters
///         );
///         
///         sql = string.Format(sql, scopeFilter);
///         
///         var employees = await _connection.QueryAsync&lt;EmployeeDto&gt;(
///             sql, 
///             parameters
///         );
///         
///         return employees.ToList();
///     }
/// }
/// </code>
/// 
/// Not Found Handling:
/// 
/// Single Entity Query:
/// <code>
/// public async Task&lt;OperatorDto?&gt; Handle(...)
/// {
///     var @operator = await _repository.GetByIdAsync(query.OperatorId);
///     
///     // Return null if not found - let API layer decide on 404
///     if (@operator is null)
///         return null;
///     
///     return new OperatorDto
///     {
///         Id = @operator.Id,
///         Username = @operator.Username,
///         // ... map properties
///     };
/// }
/// </code>
/// 
/// Collection Query:
/// <code>
/// public async Task&lt;List&lt;OperatorDto&gt;&gt; Handle(...)
/// {
///     var operators = await _repository.GetAllActiveAsync();
///     
///     // Return empty list if none found - NOT an error
///     if (!operators.Any())
///         return new List&lt;OperatorDto&gt;();
///     
///     return operators.Select(o => new OperatorDto { ... }).ToList();
/// }
/// </code>
/// 
/// Exception Handling:
/// - Database errors: Let exceptions propagate to global handler
/// - Mapping errors: Should never happen (indicates code bug)
/// - Don't catch and return null/empty (masks real problems)
/// 
/// Pipeline Execution:
/// 1. LoggingBehavior (logs query execution)
/// 2. → QueryHandler (executes query) ← YOU ARE HERE
/// 3. NO ValidationBehavior (queries not validated)
/// 4. NO UnitOfWorkBehavior (read-only, no commit)
/// 5. LoggingBehavior (logs completion)
/// 
/// Logging Best Practices:
/// <code>
/// public async Task&lt;List&lt;EmployeeDto&gt;&gt; Handle(...)
/// {
///     _logger.LogDebug(
///         "Executing SearchEmployeesQuery with SearchTerm={SearchTerm}, DepartmentId={DepartmentId}",
///         query.SearchTerm,
///         query.DepartmentId
///     );
///     
///     var employees = await ExecuteQueryAsync(query);
///     
///     _logger.LogInformation(
///         "SearchEmployeesQuery returned {Count} employees",
///         employees.Count
///     );
///     
///     return employees;
/// }
/// </code>
/// </summary>
/// <typeparam name="TQuery">Type of query to handle</typeparam>
/// <typeparam name="TResponse">Type of data to return</typeparam>
public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
}
