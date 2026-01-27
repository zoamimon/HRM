namespace HRM.Web.Models;

/// <summary>
/// ViewModel for operator list page
/// Contains paginated operators and filter/search state
/// </summary>
public sealed class OperatorListViewModel
{
    /// <summary>
    /// Paginated list of operators
    /// </summary>
    public PagedResult<OperatorSummary> Operators { get; set; } = new();

    /// <summary>
    /// Current search term (username or email)
    /// </summary>
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Current status filter (null = all)
    /// </summary>
    public string? StatusFilter { get; set; }

    /// <summary>
    /// Current page number
    /// </summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// Page size
    /// </summary>
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Available status options for filter dropdown
    /// </summary>
    public static readonly List<string> StatusOptions = new()
    {
        "Pending",
        "Active",
        "Suspended",
        "Deactivated"
    };
}
