using HRM.Modules.Identity.Domain.Entities.Permissions;
using HRM.Modules.Identity.Domain.Enums;

namespace HRM.Modules.Identity.Domain.Repositories;

/// <summary>
/// Repository interface for PermissionTemplate aggregate
/// Follows repository pattern for data access abstraction
/// </summary>
public interface IPermissionTemplateRepository
{
    /// <summary>
    /// Get all permission templates
    /// </summary>
    Task<List<PermissionTemplate>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get permission template by ID
    /// </summary>
    Task<PermissionTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get permission template by name
    /// </summary>
    Task<PermissionTemplate?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all templates applicable to users
    /// </summary>
    Task<List<PermissionTemplate>> GetUserTemplatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all templates applicable to operators
    /// </summary>
    Task<List<PermissionTemplate>> GetOperatorTemplatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get templates by category
    /// </summary>
    Task<List<PermissionTemplate>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all system templates (built-in, cannot be deleted)
    /// </summary>
    Task<List<PermissionTemplate>> GetSystemTemplatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Add new permission template
    /// </summary>
    Task AddAsync(PermissionTemplate template, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update existing permission template
    /// </summary>
    Task UpdateAsync(PermissionTemplate template, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete permission template
    /// Cannot delete system templates
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if template name exists
    /// </summary>
    Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if template is in use (assigned to any users/operators)
    /// </summary>
    Task<bool> IsInUseAsync(Guid templateId, CancellationToken cancellationToken = default);
}
