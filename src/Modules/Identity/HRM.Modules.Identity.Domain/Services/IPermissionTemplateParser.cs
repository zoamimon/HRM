using HRM.Modules.Identity.Domain.Entities.Permissions;

namespace HRM.Modules.Identity.Domain.Services;

/// <summary>
/// Service interface for parsing permission templates from XML
/// Separates parsing logic from domain logic (dependency inversion)
/// </summary>
public interface IPermissionTemplateParser
{
    /// <summary>
    /// Parse XML content to PermissionTemplate entity
    /// </summary>
    /// <param name="xmlContent">XML content to parse</param>
    /// <returns>Parsed permission template</returns>
    /// <exception cref="InvalidOperationException">If XML is invalid or malformed</exception>
    Task<PermissionTemplate> ParseAsync(string xmlContent);

    /// <summary>
    /// Parse XML file to PermissionTemplate entity
    /// </summary>
    /// <param name="xmlFilePath">Path to XML file</param>
    /// <returns>Parsed permission template</returns>
    /// <exception cref="FileNotFoundException">If file not found</exception>
    /// <exception cref="InvalidOperationException">If XML is invalid or malformed</exception>
    Task<PermissionTemplate> ParseFromFileAsync(string xmlFilePath);

    /// <summary>
    /// Serialize PermissionTemplate entity to XML content
    /// </summary>
    /// <param name="template">Permission template to serialize</param>
    /// <returns>XML content</returns>
    Task<string> SerializeAsync(PermissionTemplate template);

    /// <summary>
    /// Validate XML content against XSD schema
    /// </summary>
    /// <param name="xmlContent">XML content to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    Task<bool> ValidateAsync(string xmlContent);

    /// <summary>
    /// Validate XML content against XSD schema and return validation errors
    /// </summary>
    /// <param name="xmlContent">XML content to validate</param>
    /// <returns>List of validation error messages (empty if valid)</returns>
    Task<List<string>> ValidateAndGetErrorsAsync(string xmlContent);
}
