namespace HRM.BuildingBlocks.Application.Abstractions.Commands;

/// <summary>
/// Marker interface for commands that belong to a specific module
/// Used by UnitOfWorkBehavior for type-safe module resolution
///
/// Design Pattern: Marker Interface + Type Guard
///
/// Purpose:
/// - Enables compile-time type safety for module commands
/// - Replaces reflection-based ModuleName detection
/// - More performant than GetProperty() reflection
/// - Clear interface segregation
///
/// Usage:
/// <code>
/// // In UnitOfWorkBehavior:
/// if (request is not IHasModuleName moduleCommand)
///     return response;  // Not a module command, skip UoW
///
/// var moduleName = moduleCommand.ModuleName;
/// var unitOfWork = _unitOfWorks.Single(x => x.ModuleName == moduleName);
/// </code>
///
/// Benefits:
/// - ✅ Type-safe pattern matching (no reflection)
/// - ✅ Better performance (compiled IL check)
/// - ✅ Clear intent (explicit marker interface)
/// - ✅ Refactoring-safe (compiler errors if interface changes)
/// </summary>
public interface IHasModuleName
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
