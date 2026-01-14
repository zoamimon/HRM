using MediatR;

namespace HRM.BuildingBlocks.Application.Abstractions.Queries;

/// <summary>
/// Interface for queries in the CQRS pattern (read operations).
/// Queries return data directly WITHOUT Result wrapper.
/// 
/// CQRS Separation:
/// - Commands: Write operations → Return Result (success/failure)
/// - Queries: Read operations → Return data directly (TResponse)
/// 
/// Why Queries Don't Use Result:
/// 1. Read-only operations can't violate business rules
/// 2. Database errors are exceptional (throw exceptions, handle globally)
/// 3. Simpler API responses (no unwrapping needed)
/// 4. Not found → return null or empty collection (not an error)
/// 5. Cleaner, more concise handler code
/// 
/// Query Characteristics:
/// - Read-only (no state modification)
/// - Immutable (use record type with init-only properties)
/// - Returns DTOs (not domain entities)
/// - Can bypass domain model (query database directly)
/// - Optimized for performance (AsNoTracking, projections, Dapper)
/// - Must respect data scoping for User requests
/// 
/// Performance Optimization Techniques:
/// 1. Use AsNoTracking() for EF Core queries
/// 2. Project to DTO in database (SELECT only needed fields)
/// 3. Use Dapper for complex queries with joins
/// 4. Apply filtering at database level
/// 5. Use indexes on filter columns
/// 
/// Examples:
/// <code>
/// // Single entity query (nullable return for not found)
/// public sealed record GetOperatorByIdQuery : IQuery&lt;OperatorDto?&gt;
/// {
///     public Guid OperatorId { get; init; }
/// }
/// 
/// // Collection query
/// public sealed record GetActiveOperatorsQuery : IQuery&lt;List&lt;OperatorDto&gt;&gt;
/// {
///     public string? SearchTerm { get; init; }
/// }
/// 
/// // Filtered collection with paging
/// public sealed record SearchEmployeesQuery : IQuery&lt;PagedResult&lt;EmployeeDto&gt;&gt;
/// {
///     public string? SearchTerm { get; init; }
///     public Guid? DepartmentId { get; init; }
///     public Guid? CompanyId { get; init; }
///     public int Page { get; init; } = 1;
///     public int PageSize { get; init; } = 20;
/// }
/// 
/// // Scalar value query
/// public sealed record CountActiveUsersQuery : IQuery&lt;int&gt;
/// {
///     public Guid? CompanyId { get; init; }
/// }
/// 
/// // Complex aggregation query
/// public sealed record GetDepartmentStatisticsQuery : IQuery&lt;DepartmentStatsDto&gt;
/// {
///     public Guid DepartmentId { get; init; }
/// }
/// </code>
/// 
/// Return Type Guidelines:
/// - Single entity: OperatorDto? (nullable - null if not found)
/// - Collection: List&lt;EmployeeDto&gt; (empty list if none found)
/// - Paged results: PagedResult&lt;T&gt; (custom type with items + metadata)
/// - Scalar: int, bool, Guid, etc.
/// - Computed: Custom DTO with aggregated data
/// 
/// DTO vs Domain Entity:
/// Always return DTOs, never domain entities!
/// 
/// Benefits:
/// - API contract stability (domain changes don't break API)
/// - Security (expose only needed data)
/// - Performance (flat structure, no lazy loading)
/// - Serialization (DTOs are designed for serialization)
/// 
/// Not Found Handling:
/// - Single entity: Return null (let API layer decide on 404)
/// - Collection: Return empty list (not an error)
/// - Scalar: Return default value or throw if truly exceptional
/// 
/// Data Scoping:
/// Queries MUST respect ScopeLevel when executed by Users:
/// - Apply IDataScopingService.ApplyScopingAsync()
/// - Filters data based on User's assignments
/// - Operators see all data (no scoping)
/// 
/// Pipeline Flow:
/// 1. LoggingBehavior (logs query)
/// 2. ValidationBehavior (skipped - queries not validated by pipeline)
/// 3. → QueryHandler (data retrieval) ← YOU ARE HERE
/// 4. NO UnitOfWorkBehavior (read-only, no commit)
/// 5. LoggingBehavior (logs result)
/// </summary>
/// <typeparam name="TResponse">Type of data returned by query</typeparam>
public interface IQuery<out TResponse> : IRequest<TResponse>
{
}
