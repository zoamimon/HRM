using HRM.BuildingBlocks.Domain.Entities;
using HRM.Modules.Identity.Domain.ValueObjects;

namespace HRM.Modules.Identity.Domain.Entities.Permissions;

/// <summary>
/// Permission Template aggregate root
/// Represents a reusable permission configuration that can be assigned to users/operators
///
/// Design Philosophy:
/// - XML defines WHAT capabilities exist (declarative)
/// - Runtime evaluators determine HOW to enforce them (imperative)
/// - Templates are immutable once created (versioned for changes)
///
/// Responsibilities:
/// - Store permission structure (modules, entities, actions, scopes, constraints)
/// - Provide metadata (name, version, applicability)
/// - Validate template structure
/// - Support XML serialization/deserialization
///
/// Business Rules:
/// - Template name must be unique
/// - System templates cannot be deleted or modified
/// - Version must follow semantic versioning (major.minor format)
/// - Modules, entities, and actions must have unique names within their scope
///
/// Storage Strategy:
/// - Option 1: Store entire XML as TEXT column (simple, flexible)
/// - Option 2: Store structured data in JSON column (queryable)
/// - Option 3: Store in separate tables (normalized, complex)
///
/// Recommended: Store XML in TEXT column + cached parsed structure in memory
/// </summary>
public sealed class PermissionTemplate : Entity, IAggregateRoot
{
    /// <summary>
    /// Template metadata (name, version, description, etc.)
    /// </summary>
    public TemplateMetadata Metadata { get; private set; } = default!;

    /// <summary>
    /// Permission modules within this template
    /// Each module contains entities with actions and scopes
    /// </summary>
    public List<PermissionModule> Modules { get; private set; } = new();

    /// <summary>
    /// Original XML content (for storage and version control)
    /// Stored as-is from XML file
    /// </summary>
    public string XmlContent { get; private set; } = default!;

    /// <summary>
    /// When the template was last modified
    /// </summary>
    public DateTime LastModifiedAtUtc { get; private set; }

    /// <summary>
    /// Private constructor for EF Core
    /// </summary>
    private PermissionTemplate()
    {
    }

    /// <summary>
    /// Create a new permission template
    /// </summary>
    /// <param name="metadata">Template metadata</param>
    /// <param name="modules">Permission modules</param>
    /// <param name="xmlContent">Original XML content</param>
    /// <returns>New permission template</returns>
    public static PermissionTemplate Create(
        TemplateMetadata metadata,
        List<PermissionModule> modules,
        string xmlContent)
    {
        var template = new PermissionTemplate
        {
            Id = Guid.NewGuid(),
            Metadata = metadata,
            Modules = modules,
            XmlContent = xmlContent,
            LastModifiedAtUtc = DateTime.UtcNow
        };

        // Validate template structure
        template.Validate();

        return template;
    }

    /// <summary>
    /// Update template with new version
    /// Creates a new version rather than modifying existing
    /// (Recommended: Create new template with incremented version)
    /// </summary>
    /// <param name="metadata">Updated metadata</param>
    /// <param name="modules">Updated modules</param>
    /// <param name="xmlContent">Updated XML content</param>
    public void Update(
        TemplateMetadata metadata,
        List<PermissionModule> modules,
        string xmlContent)
    {
        // Prevent modification of system templates
        if (Metadata.IsSystem)
        {
            throw new InvalidOperationException(
                $"Cannot modify system template '{Metadata.Name}'. System templates are read-only."
            );
        }

        Metadata = metadata;
        Modules = modules;
        XmlContent = xmlContent;
        LastModifiedAtUtc = DateTime.UtcNow;

        // Validate new structure
        Validate();
    }

    /// <summary>
    /// Validate template structure
    /// Ensures data integrity and business rules
    /// </summary>
    private void Validate()
    {
        // Validate metadata
        if (string.IsNullOrWhiteSpace(Metadata.Name))
        {
            throw new InvalidOperationException("Template name is required.");
        }

        if (string.IsNullOrWhiteSpace(Metadata.Version))
        {
            throw new InvalidOperationException("Template version is required.");
        }

        // Validate version format (major.minor)
        if (!System.Text.RegularExpressions.Regex.IsMatch(Metadata.Version, @"^\d+\.\d+$"))
        {
            throw new InvalidOperationException(
                $"Template version '{Metadata.Version}' is invalid. Expected format: 'major.minor' (e.g., '1.0', '2.1')."
            );
        }

        // Validate modules
        if (!Modules.Any())
        {
            throw new InvalidOperationException("Template must have at least one module.");
        }

        // Check for duplicate module names
        var duplicateModules = Modules
            .GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateModules.Any())
        {
            throw new InvalidOperationException(
                $"Duplicate module names found: {string.Join(", ", duplicateModules)}"
            );
        }

        // Validate each module has at least one entity
        foreach (var module in Modules)
        {
            if (!module.Entities.Any())
            {
                throw new InvalidOperationException(
                    $"Module '{module.Name}' must have at least one entity."
                );
            }

            // Check for duplicate entity names within module
            var duplicateEntities = module.Entities
                .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateEntities.Any())
            {
                throw new InvalidOperationException(
                    $"Module '{module.Name}' has duplicate entity names: {string.Join(", ", duplicateEntities)}"
                );
            }

            // Validate each entity has at least one action
            foreach (var entity in module.Entities)
            {
                if (!entity.Actions.Any())
                {
                    throw new InvalidOperationException(
                        $"Entity '{module.Name}.{entity.Name}' must have at least one action."
                    );
                }

                // Check for duplicate action names within entity
                var duplicateActions = entity.Actions
                    .GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (duplicateActions.Any())
                {
                    throw new InvalidOperationException(
                        $"Entity '{module.Name}.{entity.Name}' has duplicate action names: {string.Join(", ", duplicateActions)}"
                    );
                }
            }
        }
    }

    /// <summary>
    /// Get module by name
    /// </summary>
    public PermissionModule? GetModule(string moduleName)
    {
        return Modules.FirstOrDefault(m => m.Name.Equals(moduleName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get entity by module and entity name
    /// </summary>
    public PermissionEntity? GetEntity(string moduleName, string entityName)
    {
        return GetModule(moduleName)?.GetEntity(entityName);
    }

    /// <summary>
    /// Get action by module, entity, and action name
    /// </summary>
    public PermissionAction? GetAction(string moduleName, string entityName, string actionName)
    {
        return GetEntity(moduleName, entityName)?.GetAction(actionName);
    }

    /// <summary>
    /// Check if template has specific permission
    /// </summary>
    public bool HasPermission(string moduleName, string entityName, string actionName)
    {
        return GetAction(moduleName, entityName, actionName) != null;
    }

    /// <summary>
    /// Get all modules, entities, and actions as flat list (for UI binding)
    /// </summary>
    public List<(string Module, string Entity, string Action, List<PermissionScope> Scopes)> GetAllPermissions()
    {
        var permissions = new List<(string, string, string, List<PermissionScope>)>();

        foreach (var module in Modules)
        {
            foreach (var entity in module.Entities)
            {
                foreach (var action in entity.Actions)
                {
                    permissions.Add((
                        module.Name,
                        entity.Name,
                        action.Name,
                        action.Scopes
                    ));
                }
            }
        }

        return permissions;
    }

    /// <summary>
    /// Check if template can be assigned to user
    /// </summary>
    public bool CanAssignToUser() => Metadata.CanAssignToUser();

    /// <summary>
    /// Check if template can be assigned to operator
    /// </summary>
    public bool CanAssignToOperator() => Metadata.CanAssignToOperator();
}
