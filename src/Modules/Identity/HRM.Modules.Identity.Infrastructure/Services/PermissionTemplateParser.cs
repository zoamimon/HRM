using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using HRM.BuildingBlocks.Domain.Enums;
using HRM.Modules.Identity.Domain.Entities.Permissions;
using HRM.Modules.Identity.Domain.Enums;
using HRM.Modules.Identity.Domain.Services;
using HRM.Modules.Identity.Domain.ValueObjects;

namespace HRM.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Implementation of IPermissionTemplateParser
/// Parses XML permission templates to domain entities
///
/// Design Decisions:
/// - Uses XDocument for parsing (LINQ to XML) - cleaner API than XmlDocument
/// - Supports both string content and file path inputs
/// - Optional XSD validation for schema compliance
/// - Preserves original XML for storage and version control
/// </summary>
public sealed class PermissionTemplateParser : IPermissionTemplateParser
{
    private const string XmlNamespace = "http://hrm.system/permissions";
    private readonly List<string> _validationErrors = new();

    /// <summary>
    /// Parse XML content to PermissionTemplate entity
    /// </summary>
    public async Task<PermissionTemplate> ParseAsync(string xmlContent)
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

            // Parse metadata
            var metadataElement = root.Element(ns + "Metadata")
                ?? throw new InvalidOperationException("Metadata element is required");
            var metadata = ParseMetadata(metadataElement, ns);

            // Parse permissions
            var permissionsElement = root.Element(ns + "Permissions")
                ?? throw new InvalidOperationException("Permissions element is required");
            var modules = ParseModules(permissionsElement, ns);

            // Create template
            var template = PermissionTemplate.Create(metadata, modules, xmlContent);

            return await Task.FromResult(template);
        }
        catch (XmlException ex)
        {
            throw new InvalidOperationException($"Invalid XML format: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse permission template: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Parse XML file to PermissionTemplate entity
    /// </summary>
    public async Task<PermissionTemplate> ParseFromFileAsync(string xmlFilePath)
    {
        if (!File.Exists(xmlFilePath))
        {
            throw new FileNotFoundException($"XML file not found: {xmlFilePath}");
        }

        var xmlContent = await File.ReadAllTextAsync(xmlFilePath);
        return await ParseAsync(xmlContent);
    }

    /// <summary>
    /// Serialize PermissionTemplate entity to XML content
    /// </summary>
    public async Task<string> SerializeAsync(PermissionTemplate template)
    {
        XNamespace ns = XmlNamespace;

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(ns + "PermissionTemplate",
                // Metadata
                new XElement(ns + "Metadata",
                    new XElement(ns + "Name", template.Metadata.Name),
                    new XElement(ns + "DisplayName", template.Metadata.DisplayName),
                    new XElement(ns + "Description", template.Metadata.Description),
                    new XElement(ns + "Version", template.Metadata.Version),
                    new XElement(ns + "ApplicableTo", template.Metadata.ApplicableTo.ToString()),
                    template.Metadata.Category != null
                        ? new XElement(ns + "Category", template.Metadata.Category)
                        : null,
                    template.Metadata.IsSystem
                        ? new XElement(ns + "IsSystem", template.Metadata.IsSystem)
                        : null
                ),
                // Permissions
                new XElement(ns + "Permissions",
                    template.Modules.Select(module =>
                        new XElement(ns + "Module",
                            new XAttribute("name", module.Name),
                            new XAttribute("displayName", module.DisplayName),
                            module.Entities.Select(entity =>
                                new XElement(ns + "Entity",
                                    new XAttribute("name", entity.Name),
                                    new XAttribute("displayName", entity.DisplayName),
                                    entity.Actions.Select(action =>
                                        SerializeAction(action, ns)
                                    )
                                )
                            )
                        )
                    )
                )
            )
        );

        return await Task.FromResult(doc.ToString());
    }

    /// <summary>
    /// Validate XML content against XSD schema
    /// </summary>
    public async Task<bool> ValidateAsync(string xmlContent)
    {
        var errors = await ValidateAndGetErrorsAsync(xmlContent);
        return errors.Count == 0;
    }

    /// <summary>
    /// Validate XML content against XSD schema and return validation errors
    /// </summary>
    public async Task<List<string>> ValidateAndGetErrorsAsync(string xmlContent)
    {
        var errors = new List<string>();

        try
        {
            var doc = XDocument.Parse(xmlContent);

            // TODO: Load XSD schema and validate
            // For now, just basic XML structure validation
            // In production, you would load the XSD and validate against it

            var root = doc.Root;
            if (root == null)
            {
                errors.Add("XML document has no root element");
                return await Task.FromResult(errors);
            }

            // Validate required elements
            XNamespace ns = root.GetDefaultNamespace();

            if (root.Element(ns + "Metadata") == null)
            {
                errors.Add("Metadata element is required");
            }

            if (root.Element(ns + "Permissions") == null)
            {
                errors.Add("Permissions element is required");
            }

            return await Task.FromResult(errors);
        }
        catch (XmlException ex)
        {
            errors.Add($"Invalid XML: {ex.Message}");
            return await Task.FromResult(errors);
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Parse Metadata element
    /// </summary>
    private TemplateMetadata ParseMetadata(XElement metadataElement, XNamespace ns)
    {
        var name = metadataElement.Element(ns + "Name")?.Value
            ?? throw new InvalidOperationException("Metadata.Name is required");

        var displayName = metadataElement.Element(ns + "DisplayName")?.Value
            ?? throw new InvalidOperationException("Metadata.DisplayName is required");

        var description = metadataElement.Element(ns + "Description")?.Value
            ?? throw new InvalidOperationException("Metadata.Description is required");

        var version = metadataElement.Element(ns + "Version")?.Value
            ?? throw new InvalidOperationException("Metadata.Version is required");

        var applicableToStr = metadataElement.Element(ns + "ApplicableTo")?.Value
            ?? throw new InvalidOperationException("Metadata.ApplicableTo is required");

        if (!Enum.TryParse<ApplicableTo>(applicableToStr, true, out var applicableTo))
        {
            throw new InvalidOperationException($"Invalid ApplicableTo value: {applicableToStr}");
        }

        var category = metadataElement.Element(ns + "Category")?.Value;

        var isSystemStr = metadataElement.Element(ns + "IsSystem")?.Value;
        var isSystem = bool.TryParse(isSystemStr, out var isSystemValue) && isSystemValue;

        return new TemplateMetadata(
            name,
            displayName,
            description,
            version,
            applicableTo,
            category,
            isSystem
        );
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

    /// <summary>
    /// Serialize action to XElement
    /// </summary>
    private XElement SerializeAction(PermissionAction action, XNamespace ns)
    {
        var actionElement = new XElement(ns + "Action",
            new XAttribute("name", action.Name),
            new XAttribute("displayName", action.DisplayName)
        );

        // Add defaultScope attribute if present
        if (!string.IsNullOrEmpty(action.DefaultScope))
        {
            actionElement.Add(new XAttribute("defaultScope", action.DefaultScope));
        }

        // Add scopes if present
        if (action.Scopes.Any())
        {
            actionElement.Add(new XElement(ns + "Scopes",
                action.Scopes.Select(scope =>
                    new XElement(ns + "Scope",
                        new XAttribute("value", MapScopeLevelToString(scope.Value)),
                        new XAttribute("displayName", scope.DisplayName),
                        scope.IsReadOnly ? new XAttribute("readOnly", "true") : null
                    )
                )
            ));
        }

        // Add constraints if present
        if (action.Constraints.Any())
        {
            actionElement.Add(new XElement(ns + "Constraints",
                action.Constraints.Select(constraint =>
                    new XElement(ns + "Constraint",
                        new XAttribute("type", constraint.Type.ToString()),
                        constraint.Parameters.Any()
                            ? new XElement(ns + "Parameters",
                                constraint.Parameters.Select(p =>
                                    new XElement(ns + "Parameter",
                                        new XAttribute("name", p.Key),
                                        new XAttribute("value", p.Value)
                                    )
                                )
                            )
                            : null
                    )
                )
            ));
        }

        return actionElement;
    }

    /// <summary>
    /// Map ScopeLevel enum to XML string value
    /// </summary>
    private string MapScopeLevelToString(ScopeLevel scopeLevel)
    {
        return scopeLevel switch
        {
            ScopeLevel.Company => "Company",
            ScopeLevel.Department => "Department",
            ScopeLevel.Position => "Position",
            ScopeLevel.Employee => "Self",
            _ => throw new InvalidOperationException($"Unknown scope level: {scopeLevel}")
        };
    }

    #endregion
}
