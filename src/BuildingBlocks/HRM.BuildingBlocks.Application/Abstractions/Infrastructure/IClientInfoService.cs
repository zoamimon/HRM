namespace HRM.BuildingBlocks.Application.Abstractions.Infrastructure;

/// <summary>
/// Service for accessing HTTP request context information.
/// Abstracts away ASP.NET Core infrastructure from application layer.
///
/// Clean Architecture Compliance:
/// - Application layer defines interface (this file)
/// - Infrastructure layer implements (reads from HttpContext)
/// - No direct dependency on ASP.NET Core in application layer
///
/// Purpose:
/// - Capture client information for security and audit purposes
/// - Support session management and device tracking
/// - Enable IP-based security policies
/// - Provide context for anomaly detection
///
/// Implementation Notes:
/// - Returns null when information is not available
/// - Caller decides how to handle null values
/// - No default values enforced at this layer
///
/// Usage in Command Handlers:
/// <code>
/// public class LoginCommandHandler
/// {
///     private readonly IClientInfoService _clientInfo;
///
///     public async Task&lt;Result&gt; Handle(...)
///     {
///         // Capture client info for RefreshToken entity
///         var refreshToken = RefreshToken.Create(
///             operatorId,
///             token,
///             expiry,
///             _clientInfo.IpAddress ?? "unknown",     // Handler decides fallback
///             _clientInfo.UserAgent
///         );
///     }
/// }
/// </code>
///
/// Production Considerations:
/// - Supports reverse proxy scenarios (X-Forwarded-For)
/// - Works with Azure App Service, AWS ALB/ELB, Nginx
/// - Thread-safe (scoped per request)
/// - No shared state between requests
/// </summary>
public interface IClientInfoService
{
    /// <summary>
    /// Gets the client's IP address.
    /// Returns null if cannot be determined.
    ///
    /// Important Notes:
    /// - Checks X-Forwarded-For header first (for reverse proxy/load balancer)
    /// - Falls back to direct connection IP
    /// - Returns original client IP (not proxy IP)
    ///
    /// Reverse Proxy Support:
    /// - Azure App Service: X-Forwarded-For set automatically
    /// - AWS ALB/ELB: X-Forwarded-For set automatically
    /// - Nginx: proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    /// - Cloudflare: CF-Connecting-IP or X-Forwarded-For
    ///
    /// Security:
    /// - First IP in X-Forwarded-For is original client
    /// - Subsequent IPs are proxy chain
    /// - Don't trust X-Forwarded-For without proper proxy configuration
    ///
    /// Usage:
    /// <code>
    /// var ip = _clientInfo.IpAddress;
    /// if (ip == null)
    /// {
    ///     // Handle anonymous/untrusted request
    ///     ip = "unknown";
    /// }
    /// </code>
    /// </summary>
    string? IpAddress { get; }

    /// <summary>
    /// Gets the client's User Agent string.
    /// Returns null if not available.
    ///
    /// Contains browser/device information:
    /// - Browser: "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0"
    /// - Mobile: "HRM-Mobile-App/1.2.3 (iOS 16.0)"
    /// - API Client: "PostmanRuntime/7.36.0"
    ///
    /// Use Cases:
    /// - Session/device identification in UI
    /// - Security anomaly detection (device change)
    /// - Browser compatibility checks
    /// - Analytics and monitoring
    ///
    /// Privacy Note:
    /// - User Agent can be spoofed easily
    /// - Should not be used alone for security decisions
    /// - Combined with IP and other signals for best results
    ///
    /// Usage:
    /// <code>
    /// var userAgent = _clientInfo.UserAgent;
    /// // Store for session display: "Chrome on Windows"
    /// // Or use for device fingerprinting
    /// </code>
    /// </summary>
    string? UserAgent { get; }

    /// <summary>
    /// Gets the current request path.
    /// Returns null if not available.
    ///
    /// Format: "/api/identity/auth/login"
    ///
    /// Use Cases:
    /// - Audit logging (know which endpoint was called)
    /// - Security monitoring (detect unusual access patterns)
    /// - Rate limiting per endpoint
    /// - Analytics
    ///
    /// Example Values:
    /// - "/api/identity/auth/login"
    /// - "/api/personnel/employees/123"
    /// - "/health"
    ///
    /// Note:
    /// - Does not include query string
    /// - Does not include scheme/host
    /// - Just the path component
    ///
    /// Usage:
    /// <code>
    /// var path = _clientInfo.RequestPath;
    /// // Log: "User accessed /api/identity/auth/login from 192.168.1.1"
    /// </code>
    /// </summary>
    string? RequestPath { get; }
}
