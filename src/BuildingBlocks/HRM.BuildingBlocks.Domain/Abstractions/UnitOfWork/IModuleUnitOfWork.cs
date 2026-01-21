namespace HRM.BuildingBlocks.Domain.Abstractions.UnitOfWork;

/// <summary>
/// Module-specific Unit of Work interface
/// Extends IUnitOfWork with module identification
/// 
/// Purpose:
/// - Enable UnitOfWorkBehavior to route to correct module's DbContext
/// - Support multiple modules in modular monolith
/// - Type-safe module resolution
/// 
/// Design Pattern: Module-Scoped Unit of Work
/// 
/// Each module implements this interface:
/// - Identity → IdentityDbContext : ModuleDbContext, IModuleUnitOfWork
/// - Personnel → PersonnelDbContext : ModuleDbContext, IModuleUnitOfWork
/// - Payroll → PayrollDbContext : ModuleDbContext, IModuleUnitOfWork
/// 
/// Registration Pattern:
/// <code>
/// // Each module registers IModuleUnitOfWork
/// // Identity.Infrastructure
/// services.AddScoped<IModuleUnitOfWork>(sp => 
///     sp.GetRequiredService<IdentityDbContext>());
/// 
/// // Personnel.Infrastructure
/// services.AddScoped<IModuleUnitOfWork>(sp => 
///     sp.GetRequiredService<PersonnelDbContext>());
/// 
/// // All registered UoW available via IEnumerable<IModuleUnitOfWork>
/// </code>
/// 
/// Resolution Pattern:
/// <code>
/// // UnitOfWorkBehavior
/// var uow = _unitOfWorks.Single(x => x.ModuleName == command.ModuleName);
/// await uow.CommitAsync();
/// </code>
/// 
/// Benefits:
/// - No service locator anti-pattern
/// - No keyed services (complex)
/// - No namespace parsing (brittle)
/// - Compile-time safety
/// - Easy to test
///
/// Note: ModuleName Property
/// - Inherited from IModuleContext via IUnitOfWork
/// - No need to redeclare
/// - IModuleContext.ModuleName already provides module identification
/// - Used by UnitOfWorkBehavior for routing: uow.ModuleName == command.ModuleName
/// </summary>
public interface IModuleUnitOfWork : IUnitOfWork
{
    // ModuleName inherited from IUnitOfWork -> IModuleContext
    // No additional members needed
}
