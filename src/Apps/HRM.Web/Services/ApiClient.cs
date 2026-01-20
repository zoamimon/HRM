using System.Net.Http.Json;
using System.Text.Json;
using HRM.Web.Models;

namespace HRM.Web.Services;

/// <summary>
/// HTTP client for calling HRM.Api endpoints
/// Handles API communication, error handling, and response mapping
/// </summary>
public sealed class ApiClient : IApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiClient> _logger;

    public ApiClient(IHttpClientFactory httpClientFactory, ILogger<ApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Register a new operator
    /// POST /api/identity/operators/register
    /// </summary>
    public async Task<ApiResponse<OperatorResponse>> RegisterOperatorAsync(
        RegisterOperatorRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient("HRM.Api");

            // Map to API contract (exclude ConfirmPassword)
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
                    Data = data,
                    StatusCode = (int)response.StatusCode
                };
            }

            // Handle error response
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(errorContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return new ApiResponse<OperatorResponse>
            {
                IsSuccess = false,
                ErrorCode = problemDetails?.Code ?? "Unknown",
                ErrorMessage = problemDetails?.Message ?? "An error occurred",
                StatusCode = (int)response.StatusCode
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling RegisterOperator API");
            return new ApiResponse<OperatorResponse>
            {
                IsSuccess = false,
                ErrorCode = "NetworkError",
                ErrorMessage = "Failed to connect to API. Please check if the API is running.",
                StatusCode = 500
            };
        }
    }

    /// <summary>
    /// Activate a pending operator
    /// POST /api/identity/operators/{id}/activate
    /// </summary>
    public async Task<ApiResponse<OperatorResponse>> ActivateOperatorAsync(
        Guid operatorId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient("HRM.Api");

            var response = await httpClient.PostAsync(
                $"/api/identity/operators/{operatorId}/activate",
                null,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<OperatorResponse>(cancellationToken);
                return new ApiResponse<OperatorResponse>
                {
                    IsSuccess = true,
                    Data = data,
                    StatusCode = (int)response.StatusCode
                };
            }

            // Handle error response
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(errorContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return new ApiResponse<OperatorResponse>
            {
                IsSuccess = false,
                ErrorCode = problemDetails?.Code ?? "Unknown",
                ErrorMessage = problemDetails?.Message ?? "An error occurred",
                StatusCode = (int)response.StatusCode
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling ActivateOperator API");
            return new ApiResponse<OperatorResponse>
            {
                IsSuccess = false,
                ErrorCode = "NetworkError",
                ErrorMessage = "Failed to connect to API. Please check if the API is running.",
                StatusCode = 500
            };
        }
    }

    /// <summary>
    /// Get all operators (future implementation)
    /// </summary>
    public async Task<ApiResponse<List<OperatorResponse>>> GetOperatorsAsync(
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement when API endpoint is available
        await Task.CompletedTask;
        return new ApiResponse<List<OperatorResponse>>
        {
            IsSuccess = false,
            ErrorCode = "NotImplemented",
            ErrorMessage = "This feature is not yet implemented",
            StatusCode = 501
        };
    }
}
