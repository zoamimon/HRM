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

                var problemDetails = JsonSerializer.Deserialize<ProblemDetailsResponse>(errorContent, jsonOptions);
                return new ApiResponse<OperatorResponse>
                {
                    IsSuccess = false,
                    ErrorCode = problemDetails?.Type ?? "ApiError",
                    ErrorMessage = problemDetails?.Detail ?? "An error occurred while processing your request",
                    ValidationErrors = problemDetails?.Errors
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
    /// Model for deserializing RFC 7807 Problem Details responses
    /// </summary>
    private sealed class ProblemDetailsResponse
    {
        public string? Type { get; set; }
        public string? Title { get; set; }
        public int Status { get; set; }
        public string? Detail { get; set; }
        public Dictionary<string, string[]>? Errors { get; set; }
    }
}
