namespace HRM.BuildingBlocks.Application.Abstractions.Commands;

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
/// - Type-safe, no string magic, self-documenting
///
/// Example:
/// <code>
/// public sealed record RegisterOperatorCommand(...)
///     : IModuleCommand&lt;Guid&gt;
/// {
///     public string ModuleName => "Identity";  // ✅ Self-documenting
/// }
///
/// // UnitOfWorkBehavior automatically:
/// var uow = _unitOfWorks.Single(x => x.ModuleName == command.ModuleName);
/// await uow.CommitAsync();
/// </code>
///
/// Benefits:
/// - No service locator anti-pattern
/// - No namespace-based routing (brittle)
/// - Easy to test
/// - Clear ownership (command knows its module)
/// - Refactoring-safe
/// </summary>
/// <typeparam name="TResponse">The type of data returned on success</typeparam>
public interface IModuleCommand<TResponse> : ICommand<TResponse>, IHasModuleName
{
    // ModuleName property inherited from IHasModuleName
}

/// <summary>
/// Module-specific command that doesn't return data (void operations)
/// Use this for commands like Delete, Logout, Deactivate, etc.
///
/// Example:
/// <code>
/// public sealed record LogoutCommand(string RefreshToken) : IModuleCommand
/// {
///     public string ModuleName => "Identity";
/// }
/// </code>
/// </summary>
public interface IModuleCommand : ICommand, IHasModuleName
{
    // ModuleName property inherited from IHasModuleName
}
