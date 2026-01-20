namespace HRM.Web.Models;

/// <summary>
/// Request model for registering a new operator
/// Matches the API contract from HRM.Api
/// </summary>
public sealed class RegisterOperatorRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
}
