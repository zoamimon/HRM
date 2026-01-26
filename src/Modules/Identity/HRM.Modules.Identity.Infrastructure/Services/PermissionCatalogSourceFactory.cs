using System.Reflection;
using HRM.BuildingBlocks.Domain.Abstractions.Permissions;

namespace HRM.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Factory implementation for creating IPermissionCatalogSource instances
///
/// Design:
/// - Registered as Singleton in Identity.Infrastructure DI
/// - Modules resolve this factory to create their catalog sources
/// - Encapsulates knowledge of concrete source implementations
///
/// Usage:
/// <code>
/// // In module's DI registration
/// services.AddSingleton&lt;IPermissionCatalogSource&gt;(sp =>
/// {
///     var factory = sp.GetRequiredService&lt;IPermissionCatalogSourceFactory&gt;();
///     return factory.FromEmbeddedResource(
///         typeof(ModuleAssemblyMarker).Assembly,
///         "Module.Resources.PermissionCatalog.xml");
/// });
/// </code>
/// </summary>
public sealed class PermissionCatalogSourceFactory : IPermissionCatalogSourceFactory
{
    /// <summary>
    /// Create a catalog source from an embedded XML resource
    /// </summary>
    /// <param name="assembly">Assembly containing the embedded resource</param>
    /// <param name="resourceName">Full resource name (namespace.folder.filename)</param>
    /// <returns>Permission catalog source that reads from embedded resource</returns>
    public IPermissionCatalogSource FromEmbeddedResource(Assembly assembly, string resourceName)
    {
        return new EmbeddedXmlPermissionCatalogSource(assembly, resourceName);
    }

    /// <summary>
    /// Create a catalog source from a file path
    /// </summary>
    /// <param name="filePath">Path to the XML file</param>
    /// <returns>Permission catalog source that reads from file</returns>
    public IPermissionCatalogSource FromFile(string filePath)
    {
        return new FilePermissionCatalogSource(filePath);
    }
}
