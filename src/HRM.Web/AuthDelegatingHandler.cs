using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;

namespace HRM.Web
{
    // A simple token-refresh implementation. A production-ready version
    // would need to be more robust (e.g., handle concurrent requests with SemaphoreSlim).
    public class AuthDelegatingHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;

        public AuthDelegatingHandler(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        {
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var accessToken = await httpContext.GetTokenAsync("access_token");

            if (!string.IsNullOrEmpty(accessToken))
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(accessToken);

                // Refresh the token if it's about to expire (e.g., within the next 2 minutes)
                if (jwtToken.ValidTo < DateTime.UtcNow.AddMinutes(2))
                {
                    var newTokens = await RefreshTokenAsync(httpContext);
                    if (newTokens != null)
                    {
                        accessToken = newTokens.AccessToken;
                    }
                }

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }

            return await base.SendAsync(request, cancellationToken);
        }

        private async Task<AuthResponse> RefreshTokenAsync(HttpContext httpContext)
        {
            var refreshToken = await httpContext.GetTokenAsync("refresh_token");
            if (string.IsNullOrEmpty(refreshToken)) return null;

            // This creates a new HttpClient to avoid a circular dependency loop,
            // as this handler is part of the main HttpClient pipeline.
            var client = new HttpClient();
            var refreshTokenUrl = _configuration["ApiSettings:RefreshTokenUrl"];
            var response = await client.PostAsJsonAsync(refreshTokenUrl, new { refreshToken });

            if (response.IsSuccessStatusCode)
            {
                var newTokens = await response.Content.ReadFromJsonAsync<AuthResponse>();

                // Update the authentication session with the new tokens
                var authResult = await httpContext.AuthenticateAsync();
                var authProps = authResult.Properties;
                authProps.UpdateTokenValue("access_token", newTokens.AccessToken);
                authProps.UpdateTokenValue("refresh_token", newTokens.RefreshToken);

                await httpContext.SignInAsync(authResult.Principal, authProps);

                return newTokens;
            }

            return null;
        }

        private class AuthResponse
        {
            public string AccessToken { get; set; }
            public string RefreshToken { get; set; }
        }
    }
}
