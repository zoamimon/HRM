using MediatR;

namespace HRM.BuildingBlocks.Application.Abstractions.Messaging;

/// <summary>
/// Marker interface for queries (read operations)
/// Queries return data without modifying system state
/// 
/// Examples:
/// - GetOperatorByIdQuery
/// - ListEmployeesQuery
/// - SearchDepartmentsQuery
/// 
/// CQRS Pattern:
/// - Queries are READ-ONLY
/// - No side effects
/// - Can be cached
/// - Can query read models directly
/// </summary>
/// <typeparam name="TResponse">The type of response returned by the query</typeparam>
public interface IQuery<out TResponse> : IRequest<TResponse>
{
}
