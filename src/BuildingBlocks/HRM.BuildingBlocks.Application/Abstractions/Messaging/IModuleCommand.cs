namespace HRM.BuildingBlocks.Application.Abstractions.Messaging;

/// <summary>
/// Marker interface for module-specific commands
/// Enables UnitOfWorkBehavior to route to correct module's DbContext
/// 
/// Design Pattern: Module-Scoped Unit of Work
/// 
/// Problem:
/// - Modular monolith has multiple modules (Identity, Personnel, Payroll)
/// - Each module has its own DbContext
/// - UnitOfWorkBehavior needs to know which UoW to commit
/// 
/// Solution:
/// - Command declares its module via ModuleName property
/// - Behavior resolves correct IModuleUnitOfWork at runtime
/// - Type-safe, no string magic, compile-time checked
/// 
/// Example:
/// <code>
/// public sealed record RegisterOperatorCommand(...) 
///     : IModuleCommand<Result<Guid>>
/// {
///     public string ModuleName => "Identity";  // ✅ Self-documenting
/// }
/// 
/// // UnitOfWorkBehavior
/// var uow = _unitOfWorks.Single(x => x.ModuleName == command.ModuleName);
/// await uow.CommitAsync();
/// </code>
/// 
/// Benefits:
/// - No service locator anti-pattern
/// - No namespace-based routing
/// - Easy to test
/// - Clear ownership (command knows its module)
/// - Refactoring-safe
/// </summary>
/// <typeparam name="TResponse">The type of response returned by the command</typeparam>
public interface IModuleCommand<out TResponse> : ICommand<TResponse>
{
    /// <summary>
    /// Module name that owns this command
    /// Must match IModuleUnitOfWork.ModuleName
    /// 
    /// Examples:
    /// - "Identity"
    /// - "Personnel"
    /// - "Payroll"
    /// - "Organization"
    /// </summary>
    string ModuleName { get; }
}
