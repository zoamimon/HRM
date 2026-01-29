using System.Reflection;

namespace HRM.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// Configuration for a RouteSecurityMap source
/// </summary>
public sealed class RouteSecurityMapSourceConfig
{
    /// <summary>
    /// Assembly containing the embedded resource
    /// </summary>
    public required Assembly Assembly { get; init; }

    /// <summary>
    /// Full resource name (e.g., "Namespace.Folder.RouteSecurityMap.xml")
    /// </summary>
    public required string ResourceName { get; init; }
}

/// <summary>
/// Options for route security configuration
/// Modules add their RouteSecurityMap sources to this options class
/// </summary>
public sealed class RouteSecurityOptions
{
    /// <summary>
    /// If true, routes not found in RouteSecurityMap will be denied (403)
    /// If false, routes not found will be allowed through (backward compatibility)
    ///
    /// IMPORTANT: Enterprise systems should always use DenyByDefault = true
    /// to prevent security holes when:
    /// - Developer forgets to add a new endpoint to XML
    /// - Endpoint path is renamed
    /// - New module is added without RouteSecurityMap
    ///
    /// Default: true (secure by default)
    /// </summary>
    public bool DenyByDefault { get; set; } = true;

    /// <summary>
    /// List of RouteSecurityMap sources to load at startup
    /// </summary>
    public List<RouteSecurityMapSourceConfig> Sources { get; } = new();
}
