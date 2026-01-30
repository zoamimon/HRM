using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HRM.BuildingBlocks.Domain.Abstractions.Security;
using Microsoft.Extensions.Logging;

namespace HRM.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// Service for route-based security
/// Loads and parses RouteSecurityMap.xml files from module assemblies
/// Provides route matching to determine permissions required
///
/// Performance Optimization:
/// - Route lookups are cached using ConcurrentDictionary
/// - First lookup does regex matching, subsequent lookups hit cache
/// - Cache is thread-safe for concurrent requests
/// </summary>
public sealed class RouteSecurityService : IRouteSecurityService
{
    private readonly List<PublicRouteEntry> _publicRoutes = new();
    private readonly List<RouteSecurityEntry> _protectedRoutes = new();
    private readonly ILogger<RouteSecurityService> _logger;
    private bool _isLoaded;

    // Route lookup caches - avoid regex matching on every request
    // Key format: "{METHOD}:{normalizedPath}" e.g. "GET:/api/identity/operators"
    private readonly ConcurrentDictionary<string, bool> _publicRouteCache = new();
    private readonly ConcurrentDictionary<string, RouteSecurityEntry?> _protectedRouteCache = new();

    public RouteSecurityService(ILogger<RouteSecurityService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Load RouteSecurityMap from an embedded resource
    /// Called by RouteSecurityLoaderService at startup
    /// </summary>
    public void LoadFromEmbeddedResource(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            _logger.LogWarning(
                "RouteSecurityMap not found: {ResourceName} in {Assembly}",
                resourceName,
                assembly.GetName().Name);
            return;
        }

        using var reader = new StreamReader(stream);
        var xml = reader.ReadToEnd();
        LoadFromXml(xml, resourceName);
    }

    /// <summary>
    /// Load RouteSecurityMap from XML string
    /// </summary>
    public void LoadFromXml(string xml, string sourceName = "unknown")
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var root = doc.Root;
            if (root == null)
            {
                _logger.LogWarning("RouteSecurityMap has no root element: {Source}", sourceName);
                return;
            }

            var ns = root.GetDefaultNamespace();
            var moduleName = root.Attribute("Module")?.Value ?? "Unknown";

            // Parse public routes
            var publicRoutesElement = root.Element(ns + "PublicRoutes");
            if (publicRoutesElement != null)
            {
                foreach (var routeElement in publicRoutesElement.Elements(ns + "Route"))
                {
                    var method = routeElement.Attribute("Method")?.Value ?? "GET";
                    var path = routeElement.Attribute("Path")?.Value ?? "";

                    if (string.IsNullOrWhiteSpace(path))
                        continue;

                    _publicRoutes.Add(new PublicRouteEntry
                    {
                        Method = method.ToUpperInvariant(),
                        Path = path,
                        PathPattern = ConvertPathToRegex(path)
                    });
                }
            }

            // Parse protected routes
            var protectedRoutesElement = root.Element(ns + "ProtectedRoutes");
            if (protectedRoutesElement != null)
            {
                foreach (var routeElement in protectedRoutesElement.Elements(ns + "Route"))
                {
                    var method = routeElement.Attribute("Method")?.Value ?? "GET";
                    var path = routeElement.Attribute("Path")?.Value ?? "";
                    var permission = routeElement.Attribute("Permission")?.Value ?? "";
                    var requiresDataScopeStr = routeElement.Attribute("RequiresDataScope")?.Value;

                    if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(permission))
                        continue;

                    var requiresDataScope = string.Equals(requiresDataScopeStr, "true", StringComparison.OrdinalIgnoreCase);

                    _protectedRoutes.Add(new RouteSecurityEntry
                    {
                        Method = method.ToUpperInvariant(),
                        Path = path,
                        Permission = permission,
                        RequiresDataScope = requiresDataScope,
                        PathPattern = ConvertPathToRegex(path)
                    });
                }
            }

            _isLoaded = true;
            _logger.LogInformation(
                "Loaded RouteSecurityMap from {Source}: {PublicCount} public, {ProtectedCount} protected routes",
                sourceName,
                _publicRoutes.Count,
                _protectedRoutes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse RouteSecurityMap from {Source}", sourceName);
            throw;
        }
    }

    /// <inheritdoc />
    public bool IsPublicRoute(string method, string path)
    {
        method = method.ToUpperInvariant();
        path = NormalizePath(path);
        var cacheKey = $"{method}:{path}";

        // Try cache first
        return _publicRouteCache.GetOrAdd(cacheKey, _ => LookupPublicRoute(method, path));
    }

    /// <summary>
    /// Actual public route lookup (called on cache miss)
    /// </summary>
    private bool LookupPublicRoute(string method, string path)
    {
        var isPublic = _publicRoutes.Any(r =>
            r.Method == method &&
            (r.Path.Equals(path, StringComparison.OrdinalIgnoreCase) ||
             (r.PathPattern != null && Regex.IsMatch(path, r.PathPattern, RegexOptions.IgnoreCase))));

        if (isPublic)
        {
            _logger.LogDebug("Cache miss - public route found: {Method} {Path}", method, path);
        }

        return isPublic;
    }

    /// <inheritdoc />
    public RouteSecurityEntry? GetRouteSecurityEntry(string method, string path)
    {
        method = method.ToUpperInvariant();
        path = NormalizePath(path);
        var cacheKey = $"{method}:{path}";

        // Try cache first
        return _protectedRouteCache.GetOrAdd(cacheKey, _ => LookupProtectedRoute(method, path));
    }

    /// <summary>
    /// Actual protected route lookup (called on cache miss)
    /// </summary>
    private RouteSecurityEntry? LookupProtectedRoute(string method, string path)
    {
        // Try exact match first
        var exactMatch = _protectedRoutes.FirstOrDefault(r =>
            r.Method == method &&
            r.Path.Equals(path, StringComparison.OrdinalIgnoreCase));

        if (exactMatch != null)
        {
            _logger.LogDebug("Cache miss - exact match found: {Method} {Path}", method, path);
            return exactMatch;
        }

        // Try pattern match
        var patternMatch = _protectedRoutes.FirstOrDefault(r =>
            r.Method == method &&
            r.PathPattern != null &&
            Regex.IsMatch(path, r.PathPattern, RegexOptions.IgnoreCase));

        if (patternMatch != null)
        {
            _logger.LogDebug("Cache miss - pattern match found: {Method} {Path} -> {Pattern}",
                method, path, patternMatch.Path);
        }

        return patternMatch;
    }

    /// <inheritdoc />
    public IReadOnlyList<PublicRouteEntry> GetPublicRoutes() => _publicRoutes.AsReadOnly();

    /// <inheritdoc />
    public IReadOnlyList<RouteSecurityEntry> GetProtectedRoutes() => _protectedRoutes.AsReadOnly();

    /// <inheritdoc />
    public IReadOnlyList<string> ValidateConfiguration()
    {
        var errors = new List<string>();

        if (!_isLoaded)
        {
            errors.Add("RouteSecurityMap has not been loaded");
            return errors;
        }

        // Check for duplicate routes
        var allRoutes = _publicRoutes.Select(r => $"{r.Method}:{r.Path}")
            .Concat(_protectedRoutes.Select(r => $"{r.Method}:{r.Path}"))
            .ToList();

        var duplicates = allRoutes
            .GroupBy(r => r, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var duplicate in duplicates)
        {
            errors.Add($"Duplicate route definition: {duplicate}");
        }

        // Check for routes that are both public and protected
        var publicPaths = _publicRoutes.Select(r => $"{r.Method}:{r.Path}".ToLowerInvariant()).ToHashSet();
        var protectedPaths = _protectedRoutes.Select(r => $"{r.Method}:{r.Path}".ToLowerInvariant()).ToHashSet();

        var conflicts = publicPaths.Intersect(protectedPaths).ToList();
        foreach (var conflict in conflicts)
        {
            errors.Add($"Route is defined as both public and protected: {conflict}");
        }

        if (errors.Count == 0)
        {
            _logger.LogInformation("RouteSecurityMap validation passed");
        }
        else
        {
            foreach (var error in errors)
            {
                _logger.LogError("RouteSecurityMap validation error: {Error}", error);
            }
        }

        return errors;
    }

    /// <summary>
    /// Convert a path with parameters to a regex pattern
    /// e.g., "/api/operators/{id}" -> "^/api/operators/[^/]+$"
    /// </summary>
    private static string ConvertPathToRegex(string path)
    {
        // Escape regex special characters except {param}
        var escaped = Regex.Escape(path);

        // Replace {param} with regex group that matches any non-slash characters
        var pattern = Regex.Replace(escaped, @"\\{[^}]+\\}", "[^/]+");

        // Add anchors
        return $"^{pattern}$";
    }

    /// <summary>
    /// Normalize path (remove trailing slash, ensure leading slash)
    /// </summary>
    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "/";

        // Remove query string
        var queryIndex = path.IndexOf('?');
        if (queryIndex >= 0)
            path = path[..queryIndex];

        // Ensure leading slash
        if (!path.StartsWith('/'))
            path = "/" + path;

        // Remove trailing slash (except for root)
        if (path.Length > 1 && path.EndsWith('/'))
            path = path[..^1];

        return path;
    }
}
