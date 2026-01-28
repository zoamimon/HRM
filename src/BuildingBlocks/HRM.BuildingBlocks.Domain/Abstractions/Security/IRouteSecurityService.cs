using System.Reflection;

namespace HRM.BuildingBlocks.Domain.Abstractions.Security;

/// <summary>
/// Service for route-based security
/// Determines if a route is public or protected and what permissions are required
///
/// Loading Strategy:
/// - Modules register sources via IOptions&lt;RouteSecurityOptions&gt; during DI
/// - RouteSecurityLoaderService (IHostedService) loads all sources at startup
/// - This ensures singleton is properly resolved from the real DI container
/// </summary>
public interface IRouteSecurityService
{
    /// <summary>
    /// Load RouteSecurityMap from an embedded resource
    /// </summary>
    /// <param name="assembly">Assembly containing the embedded resource</param>
    /// <param name="resourceName">Full resource name (e.g., "Namespace.Folder.FileName.xml")</param>
    void LoadFromEmbeddedResource(Assembly assembly, string resourceName);

    /// <summary>
    /// Load RouteSecurityMap from XML string
    /// </summary>
    /// <param name="xml">XML content</param>
    /// <param name="sourceName">Source name for logging</param>
    void LoadFromXml(string xml, string sourceName = "unknown");

    /// <summary>
    /// Check if a route is public (no authentication required)
    /// </summary>
    /// <param name="method">HTTP method (GET, POST, etc.)</param>
    /// <param name="path">Route path</param>
    /// <returns>True if route is public</returns>
    bool IsPublicRoute(string method, string path);

    /// <summary>
    /// Get the security entry for a protected route
    /// </summary>
    /// <param name="method">HTTP method</param>
    /// <param name="path">Route path</param>
    /// <returns>RouteSecurityEntry or null if route not found</returns>
    RouteSecurityEntry? GetRouteSecurityEntry(string method, string path);

    /// <summary>
    /// Get all public routes
    /// </summary>
    IReadOnlyList<PublicRouteEntry> GetPublicRoutes();

    /// <summary>
    /// Get all protected routes
    /// </summary>
    IReadOnlyList<RouteSecurityEntry> GetProtectedRoutes();

    /// <summary>
    /// Validate route security configuration
    /// Called at startup to catch configuration errors early
    /// </summary>
    /// <returns>List of validation errors (empty if valid)</returns>
    IReadOnlyList<string> ValidateConfiguration();
}
