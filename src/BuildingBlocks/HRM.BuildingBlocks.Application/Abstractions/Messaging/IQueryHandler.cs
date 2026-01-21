using MediatR;

namespace HRM.BuildingBlocks.Application.Abstractions.Messaging;

/// <summary>
/// Handler for queries
/// Implements data retrieval logic
/// 
/// Responsibilities:
/// - Query database (EF Core, Dapper, etc.)
/// - Map to DTOs
/// - Return results
/// - NO side effects
/// 
/// Example:
/// <code>
/// public class GetOperatorByIdQueryHandler 
///     : IQueryHandler<GetOperatorByIdQuery, Result<OperatorDto>>
/// {
///     public async Task<Result<OperatorDto>> Handle(GetOperatorByIdQuery request, ...)
///     {
///         // Query logic here
///     }
/// }
/// </code>
/// </summary>
/// <typeparam name="TQuery">The query type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
}
