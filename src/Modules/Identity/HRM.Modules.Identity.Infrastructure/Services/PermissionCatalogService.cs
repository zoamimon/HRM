using System.Xml.Linq;
using HRM.BuildingBlocks.Domain.Abstractions.Permissions;
using HRM.BuildingBlocks.Domain.Enums;
using HRM.Modules.Identity.Domain.Enums;
using HRM.Modules.Identity.Domain.Services;
using HRM.Modules.Identity.Domain.ValueObjects;
using Microsoft.Extensions.Caching.Memory;

namespace HRM.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Implementation of IPermissionCatalogService
/// Aggregates permission catalogs from multiple sources (modules)
///
/// Design Philosophy:
/// - Each module provides its own IPermissionCatalogSource
/// - This service collects and aggregates all sources
/// - Uses in-memory caching to avoid repeated parsing
/// - Catalog is read-only, loaded once at startup
///
/// Factory Pattern:
/// - Modules use IPermissionCatalogSourceFactory to create sources
/// - Sources are registered in DI as IPermissionCatalogSource
/// - This service receives IEnumerable&lt;IPermissionCatalogSource&gt;
/// </summary>
public sealed class PermissionCatalogService : IPermissionCatalogService
{
    private const string XmlNamespace = "http://hrm.system/permissions";
    private const string CatalogCacheKey = "PermissionCatalog";
    private readonly IEnumerable<IPermissionCatalogSource> _sources;
    private readonly IMemoryCache _cache;

    public PermissionCatalogService(
        IEnumerable<IPermissionCatalogSource> sources,
        IMemoryCache cache)
    {
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <summary>
    /// Load all available permissions from all catalog sources
    /// Uses in-memory cache to avoid repeated parsing
    /// </summary>
    public async Task<List<PermissionModule>> LoadCatalogAsync()
    {
        // Check cache first
        if (_cache.TryGetValue<List<PermissionModule>>(CatalogCacheKey, out var cachedModules) && cachedModules != null)
        {
            return cachedModules;
        }

        // Load from all sources
        var allModules = new List<PermissionModule>();

        foreach (var source in _sources)
        {
            try
            {
                var xmlContent = await source.LoadContentAsync();
                var modules = ParseCatalog(xmlContent);
                allModules.AddRange(modules);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to load permission catalog from source '{source.ModuleName}': {ex.Message}",
                    ex);
            }
        }

        // Validate no duplicate module names
        var duplicateModules = allModules
            .GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateModules.Count > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate module names found in permission catalogs: {string.Join(", ", duplicateModules)}");
        }

        // Cache for 1 hour (catalog rarely changes)
        _cache.Set(CatalogCacheKey, allModules, TimeSpan.FromHours(1));

        return allModules;
    }

    /// <summary>
    /// Get specific module from catalog by name
    /// </summary>
    public async Task<PermissionModule?> GetModuleAsync(string moduleName)
    {
        var modules = await LoadCatalogAsync();
        return modules.FirstOrDefault(m => m.Name.Equals(moduleName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get specific entity from catalog
    /// </summary>
    public async Task<PermissionEntity?> GetEntityAsync(string moduleName, string entityName)
    {
        var module = await GetModuleAsync(moduleName);
        return module?.GetEntity(entityName);
    }

    /// <summary>
    /// Get specific action from catalog
    /// </summary>
    public async Task<PermissionAction?> GetActionAsync(string moduleName, string entityName, string actionName)
    {
        var entity = await GetEntityAsync(moduleName, entityName);
        return entity?.GetAction(actionName);
    }

    /// <summary>
    /// Check if permission exists in catalog
    /// </summary>
    public async Task<bool> ExistsAsync(string moduleName, string entityName, string actionName)
    {
        var action = await GetActionAsync(moduleName, entityName, actionName);
        return action != null;
    }

    #region Private Helper Methods

    /// <summary>
    /// Parse catalog XML content to permission modules
    /// </summary>
    private List<PermissionModule> ParseCatalog(string xmlContent)
    {
        if (string.IsNullOrWhiteSpace(xmlContent))
        {
            throw new ArgumentException("XML content cannot be null or empty", nameof(xmlContent));
        }

        try
        {
            var doc = XDocument.Parse(xmlContent);
            var root = doc.Root ?? throw new InvalidOperationException("XML document has no root element");

            XNamespace ns = root.GetDefaultNamespace();

            var permissionsElement = root.Element(ns + "Permissions")
                ?? throw new InvalidOperationException("Permissions element is required");

            return ParseModules(permissionsElement, ns);
        }
        catch (Exception ex) when (ex is not InvalidOperationException && ex is not ArgumentException)
        {
            throw new InvalidOperationException($"Failed to parse permission catalog: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Parse Permissions/Module elements
    /// </summary>
    private List<PermissionModule> ParseModules(XElement permissionsElement, XNamespace ns)
    {
        var modules = new List<PermissionModule>();

        foreach (var moduleElement in permissionsElement.Elements(ns + "Module"))
        {
            var moduleName = moduleElement.Attribute("name")?.Value
                ?? throw new InvalidOperationException("Module name attribute is required");

            var moduleDisplayName = moduleElement.Attribute("displayName")?.Value
                ?? throw new InvalidOperationException("Module displayName attribute is required");

            var entities = ParseEntities(moduleElement, ns);

            modules.Add(new PermissionModule(moduleName, moduleDisplayName, entities));
        }

        return modules;
    }

    /// <summary>
    /// Parse Module/Entity elements
    /// </summary>
    private List<PermissionEntity> ParseEntities(XElement moduleElement, XNamespace ns)
    {
        var entities = new List<PermissionEntity>();

        foreach (var entityElement in moduleElement.Elements(ns + "Entity"))
        {
            var entityName = entityElement.Attribute("name")?.Value
                ?? throw new InvalidOperationException("Entity name attribute is required");

            var entityDisplayName = entityElement.Attribute("displayName")?.Value
                ?? throw new InvalidOperationException("Entity displayName attribute is required");

            var actions = ParseActions(entityElement, ns);

            entities.Add(new PermissionEntity(entityName, entityDisplayName, actions));
        }

        return entities;
    }

    /// <summary>
    /// Parse Entity/Action elements
    /// </summary>
    private List<PermissionAction> ParseActions(XElement entityElement, XNamespace ns)
    {
        var actions = new List<PermissionAction>();

        foreach (var actionElement in entityElement.Elements(ns + "Action"))
        {
            var actionName = actionElement.Attribute("name")?.Value
                ?? throw new InvalidOperationException("Action name attribute is required");

            var actionDisplayName = actionElement.Attribute("displayName")?.Value
                ?? throw new InvalidOperationException("Action displayName attribute is required");

            var defaultScope = actionElement.Attribute("defaultScope")?.Value;

            // Parse scopes (optional)
            var scopes = new List<PermissionScope>();
            var scopesElement = actionElement.Element(ns + "Scopes");
            if (scopesElement != null)
            {
                scopes = ParseScopes(scopesElement, ns);
            }

            // Parse constraints (optional)
            var constraints = new List<PermissionConstraint>();
            var constraintsElement = actionElement.Element(ns + "Constraints");
            if (constraintsElement != null)
            {
                constraints = ParseConstraints(constraintsElement, ns);
            }

            actions.Add(new PermissionAction(
                actionName,
                actionDisplayName,
                scopes,
                constraints,
                defaultScope
            ));
        }

        return actions;
    }

    /// <summary>
    /// Parse Action/Scopes/Scope elements
    /// </summary>
    private List<PermissionScope> ParseScopes(XElement scopesElement, XNamespace ns)
    {
        var scopes = new List<PermissionScope>();

        foreach (var scopeElement in scopesElement.Elements(ns + "Scope"))
        {
            var scopeValue = scopeElement.Attribute("value")?.Value
                ?? throw new InvalidOperationException("Scope value attribute is required");

            var scopeDisplayName = scopeElement.Attribute("displayName")?.Value
                ?? throw new InvalidOperationException("Scope displayName attribute is required");

            var readOnlyStr = scopeElement.Attribute("readOnly")?.Value;
            var readOnly = bool.TryParse(readOnlyStr, out var readOnlyValue) && readOnlyValue;

            ScopeLevel scopeLevel = scopeValue switch
            {
                "Company" => ScopeLevel.Company,
                "Department" => ScopeLevel.Department,
                "Position" => ScopeLevel.Position,
                "Self" => ScopeLevel.Employee,
                _ => throw new InvalidOperationException($"Invalid scope value: {scopeValue}")
            };

            scopes.Add(new PermissionScope(scopeLevel, scopeDisplayName, readOnly));
        }

        return scopes;
    }

    /// <summary>
    /// Parse Action/Constraints/Constraint elements
    /// </summary>
    private List<PermissionConstraint> ParseConstraints(XElement constraintsElement, XNamespace ns)
    {
        var constraints = new List<PermissionConstraint>();

        foreach (var constraintElement in constraintsElement.Elements(ns + "Constraint"))
        {
            var constraintTypeStr = constraintElement.Attribute("type")?.Value
                ?? throw new InvalidOperationException("Constraint type attribute is required");

            if (!Enum.TryParse<ConstraintType>(constraintTypeStr, true, out var constraintType))
            {
                throw new InvalidOperationException($"Invalid constraint type: {constraintTypeStr}");
            }

            // Parse parameters (optional)
            var parameters = new Dictionary<string, string>();
            var parametersElement = constraintElement.Element(ns + "Parameters");
            if (parametersElement != null)
            {
                foreach (var paramElement in parametersElement.Elements(ns + "Parameter"))
                {
                    var paramName = paramElement.Attribute("name")?.Value
                        ?? throw new InvalidOperationException("Parameter name attribute is required");

                    var paramValue = paramElement.Attribute("value")?.Value
                        ?? throw new InvalidOperationException("Parameter value attribute is required");

                    parameters[paramName] = paramValue;
                }
            }

            constraints.Add(new PermissionConstraint(constraintType, parameters));
        }

        return constraints;
    }

    #endregion
}
