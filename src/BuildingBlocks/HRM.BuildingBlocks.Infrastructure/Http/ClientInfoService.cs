using HRM.BuildingBlocks.Application.Abstractions.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace HRM.BuildingBlocks.Infrastructure.Http;

/// <summary>
/// Implementation of IClientInfoService that reads from HttpContext.
/// Provides access to client information for application layer.
///
/// Implementation Strategy:
/// - Uses IHttpContextAccessor for scoped HttpContext access
/// - Null-safe (handles cases where HttpContext is not available)
/// - Supports reverse proxy scenarios (X-Forwarded-For header)
/// - No exceptions thrown (returns null for unavailable data)
///
/// Reverse Proxy Configuration:
///
/// For Nginx:
/// <code>
/// location / {
///     proxy_pass http://backend;
///     proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
///     proxy_set_header X-Forwarded-Proto $scheme;
///     proxy_set_header Host $host;
/// }
/// </code>
///
/// For Azure App Service:
/// - X-Forwarded-For automatically set
/// - No configuration needed
///
/// For AWS ALB/ELB:
/// - X-Forwarded-For automatically set
/// - No configuration needed
///
/// Security Considerations:
/// - X-Forwarded-For can be spoofed if proxy not properly configured
/// - Ensure your reverse proxy is the only entry point
/// - Use network-level controls to prevent direct access
/// - Consider validating IP ranges for known proxies
///
/// Performance:
/// - Scoped service (one instance per request)
/// - Properties evaluated lazily
/// - No caching (HttpContext is request-scoped already)
/// - Minimal overhead (~1-2 microseconds per property access)
/// </summary>
public sealed class ClientInfoService : IClientInfoService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ClientInfoService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Gets client IP address with reverse proxy support.
    ///
    /// Resolution Order:
    /// 1. X-Forwarded-For header (first IP = original client)
    /// 2. Direct connection RemoteIpAddress
    /// 3. null (if HttpContext not available)
    ///
    /// X-Forwarded-For Format:
    /// "client_ip, proxy1_ip, proxy2_ip"
    /// We take the first IP (original client).
    ///
    /// Example:
    /// - X-Forwarded-For: "203.0.113.1, 198.51.100.1"
    /// - Returns: "203.0.113.1" (client IP)
    /// </summary>
    public string? IpAddress
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null)
            {
                return null;
            }

            // Check X-Forwarded-For header first (reverse proxy scenario)
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                // X-Forwarded-For can contain comma-separated list: "client, proxy1, proxy2"
                // First IP is the original client
                var clientIp = forwardedFor.Split(',')[0].Trim();
                if (!string.IsNullOrWhiteSpace(clientIp))
                {
                    return clientIp;
                }
            }

            // Fallback to direct connection IP
            return context.Connection.RemoteIpAddress?.ToString();
        }
    }

    /// <summary>
    /// Gets client User Agent string.
    ///
    /// Returns null if:
    /// - HttpContext not available
    /// - User-Agent header not present
    /// - User-Agent header is empty
    ///
    /// Common Values:
    /// - Web: "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36..."
    /// - Mobile: "HRM-Mobile-App/1.2.3 (iOS 16.0)"
    /// - API: "PostmanRuntime/7.36.0"
    /// </summary>
    public string? UserAgent
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null)
            {
                return null;
            }

            var userAgent = context.Request.Headers.UserAgent.ToString();
            return string.IsNullOrWhiteSpace(userAgent) ? null : userAgent;
        }
    }

    /// <summary>
    /// Gets current request path (without query string).
    ///
    /// Examples:
    /// - "/api/identity/auth/login"
    /// - "/api/personnel/employees/123"
    /// - "/" (root)
    ///
    /// Returns null if HttpContext not available.
    /// </summary>
    public string? RequestPath
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null)
            {
                return null;
            }

            return context.Request.Path.Value;
        }
    }
}
