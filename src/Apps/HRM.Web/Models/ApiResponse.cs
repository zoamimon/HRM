namespace HRM.Web.Models;

/// <summary>
/// Generic API response wrapper
/// </summary>
public sealed class ApiResponse<T>
{
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public int StatusCode { get; set; }
}

/// <summary>
/// Error response from API (Problem Details format)
/// </summary>
public sealed class ProblemDetails
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int Status { get; set; }
}
