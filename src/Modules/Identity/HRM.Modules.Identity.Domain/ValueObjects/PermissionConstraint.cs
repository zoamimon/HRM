using HRM.Modules.Identity.Domain.Enums;

namespace HRM.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Value object representing a permission constraint
/// Constraints are additional conditions that must be met for a permission to be granted
/// Immutable by design (no setters)
/// </summary>
public sealed class PermissionConstraint
{
    /// <summary>
    /// Type of constraint (ManagerOfTarget, FieldRestriction, etc.)
    /// </summary>
    public ConstraintType Type { get; private set; }

    /// <summary>
    /// Optional parameters for the constraint
    /// Stored as key-value pairs for flexibility
    ///
    /// Examples:
    /// - FieldRestriction: { "Fields": "Salary,Bonus" }
    /// - DateRange: { "MinDays": "-30", "MaxDays": "0" }
    /// - ManagerOfTarget: { "AllowIndirect": "true", "MaxLevels": "2" }
    /// </summary>
    public Dictionary<string, string> Parameters { get; private set; }

    /// <summary>
    /// Private constructor for EF Core
    /// </summary>
    private PermissionConstraint()
    {
        Parameters = new Dictionary<string, string>();
    }

    /// <summary>
    /// Create a new permission constraint
    /// </summary>
    /// <param name="type">Constraint type</param>
    /// <param name="parameters">Optional constraint parameters</param>
    public PermissionConstraint(ConstraintType type, Dictionary<string, string>? parameters = null)
    {
        Type = type;
        Parameters = parameters ?? new Dictionary<string, string>();
    }

    /// <summary>
    /// Create ManagerOfTarget constraint
    /// </summary>
    /// <param name="allowIndirect">Allow indirect management (manager of manager)</param>
    /// <param name="maxLevels">Maximum management levels to check (default: 1)</param>
    public static PermissionConstraint ManagerOfTarget(bool allowIndirect = false, int maxLevels = 1)
    {
        return new PermissionConstraint(
            ConstraintType.ManagerOfTarget,
            new Dictionary<string, string>
            {
                { "AllowIndirect", allowIndirect.ToString() },
                { "MaxLevels", maxLevels.ToString() }
            }
        );
    }

    /// <summary>
    /// Create FieldRestriction constraint
    /// </summary>
    /// <param name="restrictedFields">Comma-separated list of restricted field names</param>
    /// <param name="applyTo">Apply to View, Update, or both (default: both)</param>
    public static PermissionConstraint FieldRestriction(string restrictedFields, string applyTo = "View,Update")
    {
        return new PermissionConstraint(
            ConstraintType.FieldRestriction,
            new Dictionary<string, string>
            {
                { "Fields", restrictedFields },
                { "ApplyTo", applyTo }
            }
        );
    }

    /// <summary>
    /// Create DateRange constraint
    /// </summary>
    /// <param name="minDays">Minimum days from today (negative for past, positive for future)</param>
    /// <param name="maxDays">Maximum days from today</param>
    public static PermissionConstraint DateRange(int minDays, int maxDays)
    {
        return new PermissionConstraint(
            ConstraintType.DateRange,
            new Dictionary<string, string>
            {
                { "MinDays", minDays.ToString() },
                { "MaxDays", maxDays.ToString() }
            }
        );
    }

    /// <summary>
    /// Create WorkflowState constraint
    /// </summary>
    /// <param name="allowedStates">Comma-separated list of allowed states</param>
    public static PermissionConstraint WorkflowState(string allowedStates)
    {
        return new PermissionConstraint(
            ConstraintType.WorkflowState,
            new Dictionary<string, string>
            {
                { "AllowedStates", allowedStates }
            }
        );
    }

    /// <summary>
    /// Get parameter value by key
    /// </summary>
    public string? GetParameter(string key)
    {
        return Parameters.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// Get restricted fields (for FieldRestriction constraint)
    /// </summary>
    public List<string> GetRestrictedFields()
    {
        var fields = GetParameter("Fields");
        return string.IsNullOrEmpty(fields)
            ? new List<string>()
            : fields.Split(',', StringSplitOptions.RemoveEmptyEntries)
                   .Select(f => f.Trim())
                   .ToList();
    }

    /// <summary>
    /// Get allowed states (for WorkflowState constraint)
    /// </summary>
    public List<string> GetAllowedStates()
    {
        var states = GetParameter("AllowedStates");
        return string.IsNullOrEmpty(states)
            ? new List<string>()
            : states.Split(',', StringSplitOptions.RemoveEmptyEntries)
                   .Select(s => s.Trim())
                   .ToList();
    }
}
