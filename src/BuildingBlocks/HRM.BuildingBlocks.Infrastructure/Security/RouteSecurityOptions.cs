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
    /// List of RouteSecurityMap sources to load at startup
    /// </summary>
    public List<RouteSecurityMapSourceConfig> Sources { get; } = new();
}
