namespace HRM.Web.Models;

/// <summary>
/// Operator summary model for list view
/// Matches OperatorSummaryDto from API
/// </summary>
public sealed record OperatorSummary
{
    public Guid Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? LastLoginAtUtc { get; init; }
}
