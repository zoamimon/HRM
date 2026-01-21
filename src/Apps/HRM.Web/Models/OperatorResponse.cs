namespace HRM.Web.Models;

/// <summary>
/// Response model from operator registration API
/// </summary>
public sealed class OperatorResponse
{
    public Guid OperatorId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
