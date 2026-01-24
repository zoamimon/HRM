using HRM.Modules.Identity.Domain.ValueObjects;

namespace HRM.Modules.Identity.Domain.Services;

/// <summary>
/// Service interface for loading permission catalog
/// Permission Catalog defines all available permissions in the system
/// Admin selects permissions from catalog via UI and saves to database
/// No validation needed since permissions are selected from predefined catalog
/// </summary>
public interface IPermissionCatalogService
{
    /// <summary>
    /// Load all available permissions from the catalog
    /// Returns complete list of modules with entities and actions
    /// </summary>
    /// <returns>List of permission modules from catalog</returns>
    Task<List<PermissionModule>> LoadCatalogAsync();

    /// <summary>
    /// Get specific module from catalog by name
    /// </summary>
    /// <param name="moduleName">Module name to find</param>
    /// <returns>Permission module if found, null otherwise</returns>
    Task<PermissionModule?> GetModuleAsync(string moduleName);

    /// <summary>
    /// Get specific entity from catalog
    /// </summary>
    /// <param name="moduleName">Module name</param>
    /// <param name="entityName">Entity name</param>
    /// <returns>Permission entity if found, null otherwise</returns>
    Task<PermissionEntity?> GetEntityAsync(string moduleName, string entityName);

    /// <summary>
    /// Get specific action from catalog
    /// </summary>
    /// <param name="moduleName">Module name</param>
    /// <param name="entityName">Entity name</param>
    /// <param name="actionName">Action name</param>
    /// <returns>Permission action if found, null otherwise</returns>
    Task<PermissionAction?> GetActionAsync(string moduleName, string entityName, string actionName);

    /// <summary>
    /// Check if permission exists in catalog
    /// </summary>
    /// <param name="moduleName">Module name</param>
    /// <param name="entityName">Entity name</param>
    /// <param name="actionName">Action name</param>
    /// <returns>True if permission exists in catalog</returns>
    Task<bool> ExistsAsync(string moduleName, string entityName, string actionName);
}
