using System.Xml.Linq;
using HRM.BuildingBlocks.Domain.Enums;
using HRM.Modules.Identity.Domain.Enums;
using HRM.Modules.Identity.Domain.Services;
using HRM.Modules.Identity.Domain.ValueObjects;
using Microsoft.Extensions.Caching.Memory;

namespace HRM.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Implementation of IPermissionCatalogService
/// Loads permission catalog from XML file
/// Uses in-memory caching to avoid repeated file I/O
///
/// Design Philosophy:
/// - Permission Catalog defines ALL available permissions in system
/// - Admin selects from catalog via UI and saves to DB
/// - No validation needed - permissions come from predefined catalog
/// - Catalog is read-only and loaded once at startup
/// </summary>
public sealed class PermissionCatalogService : IPermissionCatalogService
{
    private const string XmlNamespace = "http://hrm.system/permissions";
    private const string CatalogCacheKey = "PermissionCatalog";
    private readonly string _catalogFilePath;
    private readonly IMemoryCache _cache;

    public PermissionCatalogService(IMemoryCache cache, string catalogFilePath = "templates/permissions/PermissionCatalog.xml")
    {
        _cache = cache;
        _catalogFilePath = catalogFilePath;
    }

    /// <summary>
    /// Load all available permissions from the catalog
    /// Uses in-memory cache to avoid repeated parsing
    /// </summary>
    public async Task<List<PermissionModule>> LoadCatalogAsync()
    {
        // Check cache first
        if (_cache.TryGetValue<List<PermissionModule>>(CatalogCacheKey, out var cachedModules) && cachedModules != null)
        {
            return cachedModules;
        }

        // Load from file
        if (!File.Exists(_catalogFilePath))
        {
            throw new FileNotFoundException($"Permission catalog not found at: {_catalogFilePath}");
        }

        var xmlContent = await File.ReadAllTextAsync(_catalogFilePath);
        var modules = await ParseCatalogAsync(xmlContent);

        // Cache for 1 hour (catalog rarely changes)
        _cache.Set(CatalogCacheKey, modules, TimeSpan.FromHours(1));

        return modules;
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
    /// Similar to PermissionTemplateParser but without metadata parsing
    /// </summary>
    private async Task<List<PermissionModule>> ParseCatalogAsync(string xmlContent)
    {
        if (string.IsNullOrWhiteSpace(xmlContent))
        {
            throw new ArgumentException("XML content cannot be null or empty", nameof(xmlContent));
        }

        try
        {
            // Parse XML document
            var doc = XDocument.Parse(xmlContent);
            var root = doc.Root ?? throw new InvalidOperationException("XML document has no root element");

            // Get namespace
            XNamespace ns = root.GetDefaultNamespace();

            // Parse permissions (no metadata in catalog)
            var permissionsElement = root.Element(ns + "Permissions")
                ?? throw new InvalidOperationException("Permissions element is required");

            var modules = ParseModules(permissionsElement, ns);

            return await Task.FromResult(modules);
        }
        catch (Exception ex)
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

            // Map string to ScopeLevel enum
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
