using MediatR;

namespace HRM.BuildingBlocks.Application.Abstractions.Messaging;

/// <summary>
/// Handler for commands
/// Implements business logic for write operations
/// 
/// Responsibilities:
/// - Validate business rules
/// - Coordinate domain objects
/// - Persist changes via repositories
/// - Return result
/// 
/// Example:
/// <code>
/// public class RegisterOperatorCommandHandler 
///     : ICommandHandler<RegisterOperatorCommand, Result<Guid>>
/// {
///     public async Task<Result<Guid>> Handle(RegisterOperatorCommand request, ...)
///     {
///         // Business logic here
///     }
/// }
/// </code>
/// </summary>
/// <typeparam name="TCommand">The command type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
}
