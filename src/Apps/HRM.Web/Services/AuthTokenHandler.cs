using System.Security.Claims;

namespace HRM.Web.Services;

/// <summary>
/// HTTP message handler that automatically attaches Bearer token to outgoing API requests
/// Extracts access token from current user's claims and adds Authorization header
/// </summary>
public class AuthTokenHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuthTokenHandler> _logger;

    public AuthTokenHandler(
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuthTokenHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Get access token from current user's claims
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            var accessToken = httpContext.User.FindFirst("AccessToken")?.Value;

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                // Add Authorization header with Bearer token
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                _logger.LogDebug("Added Bearer token to request: {RequestUri}", request.RequestUri);
            }
            else
            {
                _logger.LogWarning("User is authenticated but AccessToken claim is missing");
            }
        }
        else
        {
            _logger.LogDebug("User is not authenticated, skipping token attachment for: {RequestUri}", request.RequestUri);
        }

        // Continue with the request
        return await base.SendAsync(request, cancellationToken);
    }
}
