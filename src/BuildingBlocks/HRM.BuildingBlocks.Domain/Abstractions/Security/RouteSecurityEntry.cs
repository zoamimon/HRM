using HRM.BuildingBlocks.Domain.Enums;

namespace HRM.BuildingBlocks.Domain.Abstractions.Security;

/// <summary>
/// Represents a route security configuration entry
/// Maps an HTTP route to its required permission and minimum scope
/// </summary>
public sealed record RouteSecurityEntry
{
    /// <summary>
    /// HTTP method (GET, POST, PUT, DELETE, PATCH)
    /// </summary>
    public required string Method { get; init; }

    /// <summary>
    /// Route path pattern (e.g., "/api/identity/operators/{id}")
    /// Supports path parameters with {param} syntax
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Required permission key (e.g., "Identity.Operator.View")
    /// </summary>
    public required string Permission { get; init; }

    /// <summary>
    /// Minimum scope level required to access this route
    /// User's scope must be &lt;= MinScope (lower number = wider access)
    /// </summary>
    public required ScopeLevel MinScope { get; init; }

    /// <summary>
    /// Compiled regex pattern for route matching
    /// Generated from Path at load time
    /// </summary>
    public string? PathPattern { get; init; }
}

/// <summary>
/// Represents a public route that doesn't require authentication
/// </summary>
public sealed record PublicRouteEntry
{
    /// <summary>
    /// HTTP method (GET, POST, PUT, DELETE, PATCH)
    /// </summary>
    public required string Method { get; init; }

    /// <summary>
    /// Route path pattern
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Compiled regex pattern for route matching
    /// </summary>
    public string? PathPattern { get; init; }
}
