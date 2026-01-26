using System.Reflection;

namespace HRM.BuildingBlocks.Domain.Abstractions.Permissions;

/// <summary>
/// Represents a source of permission catalog data
/// Each module can provide its own permission catalog through this abstraction
///
/// Design:
/// - Modules own their permission definitions (embedded XML resources)
/// - Identity module aggregates all sources
/// - Dependency inversion: modules depend only on this abstraction
/// </summary>
public interface IPermissionCatalogSource
{
    /// <summary>
    /// Get the module name this source provides permissions for
    /// Used to identify and group permissions by module
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Load the raw XML content from the source
    /// Content follows PermissionCatalog.xsd schema
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>XML content as string</returns>
    Task<string> LoadContentAsync(CancellationToken cancellationToken = default);
}
