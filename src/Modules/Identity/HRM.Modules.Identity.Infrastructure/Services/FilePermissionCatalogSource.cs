using System.Xml.Linq;
using HRM.BuildingBlocks.Domain.Abstractions.Permissions;

namespace HRM.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Permission catalog source that reads from a file path
///
/// Design:
/// - Used for Identity module's centralized catalog
/// - Also useful for testing with file-based catalogs
/// - Module name extracted from XML content
/// </summary>
public sealed class FilePermissionCatalogSource : IPermissionCatalogSource
{
    private readonly string _filePath;
    private string? _cachedModuleName;

    public FilePermissionCatalogSource(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));

        if (!File.Exists(_filePath))
        {
            throw new FileNotFoundException(
                $"Permission catalog file not found: {_filePath}",
                _filePath);
        }
    }

    /// <summary>
    /// Module name extracted from the XML content
    /// Cached after first load
    /// </summary>
    public string ModuleName
    {
        get
        {
            if (_cachedModuleName != null)
            {
                return _cachedModuleName;
            }

            var content = File.ReadAllText(_filePath);
            _cachedModuleName = ExtractModuleName(content);

            return _cachedModuleName;
        }
    }

    /// <summary>
    /// Load the XML content from the file
    /// </summary>
    public async Task<string> LoadContentAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            throw new FileNotFoundException(
                $"Permission catalog file not found: {_filePath}",
                _filePath);
        }

        var content = await File.ReadAllTextAsync(_filePath, cancellationToken);

        // Cache module name if not already cached
        _cachedModuleName ??= ExtractModuleName(content);

        return content;
    }

    /// <summary>
    /// Extract module name from XML content
    /// </summary>
    private static string ExtractModuleName(string xmlContent)
    {
        try
        {
            var doc = XDocument.Parse(xmlContent);
            var root = doc.Root ?? throw new InvalidOperationException("XML has no root element");
            XNamespace ns = root.GetDefaultNamespace();

            var permissionsElement = root.Element(ns + "Permissions");
            var moduleElement = permissionsElement?.Element(ns + "Module");
            var moduleName = moduleElement?.Attribute("name")?.Value;

            return moduleName ?? throw new InvalidOperationException(
                "Could not extract module name from XML. Ensure Module element has 'name' attribute.");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Failed to parse XML content: {ex.Message}", ex);
        }
    }
}
