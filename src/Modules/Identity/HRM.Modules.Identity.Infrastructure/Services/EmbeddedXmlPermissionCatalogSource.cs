using System.Reflection;
using System.Xml.Linq;
using HRM.BuildingBlocks.Domain.Abstractions.Permissions;

namespace HRM.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Permission catalog source that reads from embedded XML resources
///
/// Design:
/// - Each module embeds its PermissionCatalog.xml as a resource
/// - This class loads the content from the assembly's manifest resources
/// - Module name is extracted from the XML content
/// </summary>
public sealed class EmbeddedXmlPermissionCatalogSource : IPermissionCatalogSource
{
    private const string XmlNamespace = "http://hrm.system/permissions";

    private readonly Assembly _assembly;
    private readonly string _resourceName;
    private string? _cachedModuleName;

    public EmbeddedXmlPermissionCatalogSource(Assembly assembly, string resourceName)
    {
        _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        _resourceName = resourceName ?? throw new ArgumentNullException(nameof(resourceName));

        // Validate resource exists
        var resourceNames = _assembly.GetManifestResourceNames();
        if (!resourceNames.Contains(_resourceName))
        {
            throw new ArgumentException(
                $"Embedded resource '{_resourceName}' not found in assembly '{_assembly.GetName().Name}'. " +
                $"Available resources: {string.Join(", ", resourceNames)}",
                nameof(resourceName));
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

            // Load synchronously for property access
            using var stream = _assembly.GetManifestResourceStream(_resourceName);
            if (stream == null)
            {
                throw new InvalidOperationException($"Failed to load resource stream: {_resourceName}");
            }

            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();
            _cachedModuleName = ExtractModuleName(content);

            return _cachedModuleName;
        }
    }

    /// <summary>
    /// Load the XML content from the embedded resource
    /// </summary>
    public async Task<string> LoadContentAsync(CancellationToken cancellationToken = default)
    {
        await using var stream = _assembly.GetManifestResourceStream(_resourceName);
        if (stream == null)
        {
            throw new InvalidOperationException(
                $"Failed to load embedded resource '{_resourceName}' from assembly '{_assembly.GetName().Name}'");
        }

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(cancellationToken);

        // Cache module name if not already cached
        _cachedModuleName ??= ExtractModuleName(content);

        return content;
    }

    /// <summary>
    /// Extract module name from XML content
    /// Expects first Module element's name attribute
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
