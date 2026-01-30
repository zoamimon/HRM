namespace HRM.BuildingBlocks.Domain.Abstractions.Security;

/// <summary>
/// Represents a route security configuration entry.
/// Maps an HTTP route to its required permission.
///
/// Design (separation of concerns):
/// - Permission (action): "Can this user do this?" → Identity answers
/// - Data Scope (data range): "What data can they see?" → Business module answers
///
/// Route security ONLY handles permission checks.
/// RequiresDataScope is a flag — the actual scope resolution happens
/// in the business module via IDataScopeService, NOT here.
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
    /// Whether this route requires data scope filtering.
    /// If true, downstream query handlers should resolve scope
    /// via IDataScopeService (business module).
    /// </summary>
    public bool RequiresDataScope { get; init; }

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
