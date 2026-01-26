namespace HRM.Modules.Identity.Application;

/// <summary>
/// Assembly marker for Identity.Application module
/// Used to locate embedded resources and register services
///
/// Usage:
/// <code>
/// // Get assembly for embedded resources
/// var assembly = typeof(IdentityApplicationAssemblyMarker).Assembly;
///
/// // Register permission catalog source
/// factory.FromEmbeddedResource(
///     typeof(IdentityApplicationAssemblyMarker).Assembly,
///     "HRM.Modules.Identity.Application.Resources.PermissionCatalog.xml");
/// </code>
/// </summary>
public sealed class IdentityApplicationAssemblyMarker;
