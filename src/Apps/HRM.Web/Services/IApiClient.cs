using HRM.Web.Models;

namespace HRM.Web.Services;

/// <summary>
/// Interface for calling HRM.Api endpoints
/// </summary>
public interface IApiClient
{
    /// <summary>
    /// Register a new operator
    /// </summary>
    Task<ApiResponse<OperatorResponse>> RegisterOperatorAsync(RegisterOperatorRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activate a pending operator
    /// </summary>
    Task<ApiResponse<OperatorResponse>> ActivateOperatorAsync(Guid operatorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all operators (future)
    /// </summary>
    Task<ApiResponse<List<OperatorResponse>>> GetOperatorsAsync(CancellationToken cancellationToken = default);
}
