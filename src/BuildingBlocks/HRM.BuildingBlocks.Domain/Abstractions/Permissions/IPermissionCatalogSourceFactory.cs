using System.Reflection;

namespace HRM.BuildingBlocks.Domain.Abstractions.Permissions;

/// <summary>
/// Factory for creating IPermissionCatalogSource instances
///
/// Design Philosophy:
/// - Modules reference only BuildingBlocks (this factory interface)
/// - Identity.Infrastructure provides the implementation
/// - Dependency Inversion: modules don't know about XML parsing details
///
/// Usage in modules:
/// <code>
/// public static IServiceCollection AddPersonnelPermissions(this IServiceCollection services)
/// {
///     services.AddSingleton&lt;IPermissionCatalogSource&gt;(sp =>
///     {
///         var factory = sp.GetRequiredService&lt;IPermissionCatalogSourceFactory&gt;();
///         return factory.FromEmbeddedResource(
///             typeof(PersonnelAssemblyMarker).Assembly,
///             "Personnel.Application.Resources.PermissionCatalog.xml");
///     });
///     return services;
/// }
/// </code>
/// </summary>
public interface IPermissionCatalogSourceFactory
{
    /// <summary>
    /// Create a catalog source from an embedded XML resource
    /// </summary>
    /// <param name="assembly">Assembly containing the embedded resource</param>
    /// <param name="resourceName">Full resource name (namespace + filename)</param>
    /// <returns>Permission catalog source instance</returns>
    IPermissionCatalogSource FromEmbeddedResource(Assembly assembly, string resourceName);

    /// <summary>
    /// Create a catalog source from a file path
    /// Primarily for Identity module's centralized catalog or testing
    /// </summary>
    /// <param name="filePath">Path to the XML file</param>
    /// <returns>Permission catalog source instance</returns>
    IPermissionCatalogSource FromFile(string filePath);
}
