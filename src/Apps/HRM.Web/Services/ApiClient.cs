using HRM.Web.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRM.Web.Services;

/// <summary>
/// HTTP client for calling HRM.Api endpoints
/// Handles serialization, error handling, and response mapping
/// </summary>
public interface IApiClient
{
    Task<ApiResponse<OperatorResponse>> RegisterOperatorAsync(
        RegisterOperatorRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<LoginResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<object>> LogoutAsync(
        CancellationToken cancellationToken = default);

    Task<ApiResponse<PagedResult<OperatorSummary>>> GetOperatorsAsync(
        string? searchTerm = null,
        string? status = null,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
}

public sealed class ApiClient : IApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiClient> _logger;

    public ApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<ApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Register a new operator via HRM.Api
    /// </summary>
    public async Task<ApiResponse<OperatorResponse>> RegisterOperatorAsync(
        RegisterOperatorRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient("HRM.Api");

            // Map to API contract (remove ConfirmPassword, include FullName and PhoneNumber)
            var apiRequest = new
            {
                request.Username,
                request.Email,
                request.Password,
                request.FullName,
                request.PhoneNumber
            };

            var response = await httpClient.PostAsJsonAsync(
                "/api/identity/operators/register",
                apiRequest,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<OperatorResponse>(cancellationToken);
                return new ApiResponse<OperatorResponse>
                {
                    IsSuccess = true,
                    Data = data
                };
            }

            // Handle error responses (400, 500, etc.)
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);

            // Try to parse ProblemDetails (RFC 7807)
            try
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var apiError = JsonSerializer.Deserialize<ApiErrorResponse>(errorContent, jsonOptions);
                return new ApiResponse<OperatorResponse>
                {
                    IsSuccess = false,
                    ErrorCode = apiError?.GetErrorCode() ?? "ApiError",
                    ErrorMessage = apiError?.GetErrorMessage() ?? "An error occurred while processing your request",
                    ValidationErrors = apiError?.GetValidationErrors()
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize ProblemDetails. Raw response: {ErrorContent}", errorContent);

                // Fallback if not ProblemDetails format
                return new ApiResponse<OperatorResponse>
                {
                    IsSuccess = false,
                    ErrorCode = "ApiError",
                    ErrorMessage = $"Server returned {(int)response.StatusCode}: {errorContent}"
                };
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error while calling HRM.Api");
            return new ApiResponse<OperatorResponse>
            {
                IsSuccess = false,
                ErrorCode = "NetworkError",
                ErrorMessage = "Failed to connect to API server. Please try again later."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while calling HRM.Api");
            return new ApiResponse<OperatorResponse>
            {
                IsSuccess = false,
                ErrorCode = "UnexpectedError",
                ErrorMessage = "An unexpected error occurred. Please contact support."
            };
        }
    }

    /// <summary>
    /// Login operator via HRM.Api
    /// </summary>
    public async Task<ApiResponse<LoginResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient("HRM.Api");

            var apiRequest = new
            {
                request.UsernameOrEmail,
                request.Password,
                request.RememberMe
            };

            var response = await httpClient.PostAsJsonAsync(
                "/api/identity/auth/login",
                apiRequest,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
                return new ApiResponse<LoginResponse>
                {
                    IsSuccess = true,
                    Data = data
                };
            }

            // Handle error responses
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);

            try
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var apiError = JsonSerializer.Deserialize<ApiErrorResponse>(errorContent, jsonOptions);
                return new ApiResponse<LoginResponse>
                {
                    IsSuccess = false,
                    ErrorCode = apiError?.GetErrorCode() ?? "LoginError",
                    ErrorMessage = apiError?.GetErrorMessage() ?? "Invalid username or password",
                    ValidationErrors = apiError?.GetValidationErrors()
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize ProblemDetails. Raw response: {ErrorContent}", errorContent);

                return new ApiResponse<LoginResponse>
                {
                    IsSuccess = false,
                    ErrorCode = "LoginError",
                    ErrorMessage = $"Server returned {(int)response.StatusCode}: {errorContent}"
                };
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error while calling HRM.Api");
            return new ApiResponse<LoginResponse>
            {
                IsSuccess = false,
                ErrorCode = "NetworkError",
                ErrorMessage = "Failed to connect to API server. Please try again later."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while calling HRM.Api");
            return new ApiResponse<LoginResponse>
            {
                IsSuccess = false,
                ErrorCode = "UnexpectedError",
                ErrorMessage = "An unexpected error occurred. Please contact support."
            };
        }
    }

    /// <summary>
    /// Logout current operator via HRM.Api
    /// </summary>
    public async Task<ApiResponse<object>> LogoutAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient("HRM.Api");

            var response = await httpClient.PostAsync(
                "/api/identity/auth/logout",
                null,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return new ApiResponse<object>
                {
                    IsSuccess = true,
                    Data = new { message = "Logged out successfully" }
                };
            }

            // Handle error responses
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);

            try
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var apiError = JsonSerializer.Deserialize<ApiErrorResponse>(errorContent, jsonOptions);
                return new ApiResponse<object>
                {
                    IsSuccess = false,
                    ErrorCode = apiError?.GetErrorCode() ?? "LogoutError",
                    ErrorMessage = apiError?.GetErrorMessage() ?? "Failed to logout",
                    ValidationErrors = apiError?.GetValidationErrors()
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize ProblemDetails. Raw response: {ErrorContent}", errorContent);

                return new ApiResponse<object>
                {
                    IsSuccess = false,
                    ErrorCode = "LogoutError",
                    ErrorMessage = $"Server returned {(int)response.StatusCode}: {errorContent}"
                };
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error while calling HRM.Api");
            return new ApiResponse<object>
            {
                IsSuccess = false,
                ErrorCode = "NetworkError",
                ErrorMessage = "Failed to connect to API server. Please try again later."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while calling HRM.Api");
            return new ApiResponse<object>
            {
                IsSuccess = false,
                ErrorCode = "UnexpectedError",
                ErrorMessage = "An unexpected error occurred. Please contact support."
            };
        }
    }

    /// <summary>
    /// Get paginated list of operators via HRM.Api
    /// </summary>
    public async Task<ApiResponse<PagedResult<OperatorSummary>>> GetOperatorsAsync(
        string? searchTerm = null,
        string? status = null,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient("HRM.Api");

            // Build query string
            var queryParams = new List<string>();
            if (!string.IsNullOrWhiteSpace(searchTerm))
                queryParams.Add($"searchTerm={Uri.EscapeDataString(searchTerm)}");
            if (!string.IsNullOrWhiteSpace(status))
                queryParams.Add($"status={Uri.EscapeDataString(status)}");
            queryParams.Add($"pageNumber={pageNumber}");
            queryParams.Add($"pageSize={pageSize}");

            var queryString = string.Join("&", queryParams);
            var url = $"/api/identity/operators?{queryString}";

            var response = await httpClient.GetAsync(url, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var data = await response.Content.ReadFromJsonAsync<PagedResult<OperatorSummary>>(jsonOptions, cancellationToken);
                return new ApiResponse<PagedResult<OperatorSummary>>
                {
                    IsSuccess = true,
                    Data = data ?? new PagedResult<OperatorSummary>()
                };
            }

            // Handle error responses
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);

            try
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var apiError = JsonSerializer.Deserialize<ApiErrorResponse>(errorContent, jsonOptions);
                return new ApiResponse<PagedResult<OperatorSummary>>
                {
                    IsSuccess = false,
                    ErrorCode = apiError?.GetErrorCode() ?? "ApiError",
                    ErrorMessage = apiError?.GetErrorMessage() ?? "Failed to retrieve operators",
                    ValidationErrors = apiError?.GetValidationErrors()
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize ProblemDetails. Raw response: {ErrorContent}", errorContent);

                return new ApiResponse<PagedResult<OperatorSummary>>
                {
                    IsSuccess = false,
                    ErrorCode = "ApiError",
                    ErrorMessage = $"Server returned {(int)response.StatusCode}: {errorContent}"
                };
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error while calling HRM.Api");
            return new ApiResponse<PagedResult<OperatorSummary>>
            {
                IsSuccess = false,
                ErrorCode = "NetworkError",
                ErrorMessage = "Failed to connect to API server. Please try again later."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while calling HRM.Api");
            return new ApiResponse<PagedResult<OperatorSummary>>
            {
                IsSuccess = false,
                ErrorCode = "UnexpectedError",
                ErrorMessage = "An unexpected error occurred. Please contact support."
            };
        }
    }

    /// <summary>
    /// Model for deserializing API error responses
    /// Supports both RFC 7807 Problem Details and DomainError format
    /// </summary>
    private sealed class ApiErrorResponse
    {
        // RFC 7807 Problem Details format
        public string? Type { get; set; }
        public string? Title { get; set; }
        public int Status { get; set; }
        public string? Detail { get; set; }
        public Dictionary<string, string[]>? Errors { get; set; }

        // DomainError format (from HRM.Api)
        public string? Code { get; set; }
        public string? Message { get; set; }
        public Dictionary<string, string[]>? Details { get; set; }

        /// <summary>
        /// Get error code from either format
        /// </summary>
        public string GetErrorCode() => Code ?? Type ?? "ApiError";

        /// <summary>
        /// Get error message from either format
        /// </summary>
        public string GetErrorMessage() => Message ?? Detail ?? Title ?? "An error occurred";

        /// <summary>
        /// Get validation errors from either format
        /// </summary>
        public Dictionary<string, string[]>? GetValidationErrors() => Details ?? Errors;
    }
}
