namespace HRM.Web.Models;

/// <summary>
/// Response model from HRM.Api when operator operations succeed
/// </summary>
public sealed class OperatorResponse
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsTwoFactorEnabled { get; set; }
    public DateTime? ActivatedAtUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
}
