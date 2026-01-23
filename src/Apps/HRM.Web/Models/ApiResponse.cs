namespace HRM.Web.Models;

/// <summary>
/// Generic wrapper for API responses
/// Handles both success and error cases
/// </summary>
/// <typeparam name="T">Type of data in successful response</typeparam>
public sealed class ApiResponse<T>
{
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, string[]>? ValidationErrors { get; set; }
}
