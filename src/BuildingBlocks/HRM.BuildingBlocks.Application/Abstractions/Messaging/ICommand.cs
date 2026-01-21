using MediatR;

namespace HRM.BuildingBlocks.Application.Abstractions.Messaging;

/// <summary>
/// Marker interface for commands (write operations)
/// Commands represent actions that change system state
/// 
/// Examples:
/// - RegisterOperatorCommand
/// - UpdateEmployeeCommand
/// - DeleteDepartmentCommand
/// 
/// CQRS Pattern:
/// - Commands: Write operations (Create, Update, Delete)
/// - Queries: Read operations (Get, List, Search)
/// </summary>
/// <typeparam name="TResponse">The type of response returned by the command</typeparam>
public interface ICommand<out TResponse> : IRequest<TResponse>
{
}
