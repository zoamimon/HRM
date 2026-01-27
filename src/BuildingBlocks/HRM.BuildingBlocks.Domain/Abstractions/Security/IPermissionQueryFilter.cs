using System.Linq.Expressions;

namespace HRM.BuildingBlocks.Domain.Abstractions.Security;

/// <summary>
/// Interface for permission-based query filters
/// Applies data-level security based on user's permission scope
///
/// Usage:
/// Each entity that needs scope-based filtering implements a filter class
/// The filter is automatically applied to queries via middleware or service
///
/// Example:
/// <code>
/// public class OperatorViewFilter : IPermissionQueryFilter&lt;Operator&gt;
/// {
///     public string Permission => "Identity.Operator.View";
///
///     public Expression&lt;Func&lt;Operator, bool&gt;&gt; Build(PermissionFilterContext context)
///     {
///         return context.Scope switch
///         {
///             PermissionScope.Global => o => true,
///             _ => o => false // Operators only visible at Global scope
///         };
///     }
/// }
/// </code>
/// </summary>
/// <typeparam name="TEntity">Entity type to filter</typeparam>
public interface IPermissionQueryFilter<TEntity> where TEntity : class
{
    /// <summary>
    /// Permission key this filter applies to (e.g., "Identity.Operator.View")
    /// </summary>
    string Permission { get; }

    /// <summary>
    /// Build the filter expression based on user context
    /// </summary>
    /// <param name="context">User context with permission and scope</param>
    /// <returns>Expression to filter entities</returns>
    Expression<Func<TEntity, bool>> Build(PermissionFilterContext context);
}

/// <summary>
/// Context for building permission query filters
/// Contains user information and their scope for the current permission
/// </summary>
public sealed record PermissionFilterContext
{
    /// <summary>
    /// Current user ID
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// Current permission being checked
    /// </summary>
    public required string Permission { get; init; }

    /// <summary>
    /// User's scope for this permission
    /// </summary>
    public required PermissionScope Scope { get; init; }

    /// <summary>
    /// User's department ID (for Department scope filtering)
    /// </summary>
    public Guid? DepartmentId { get; init; }

    /// <summary>
    /// User's company ID (for Company scope filtering)
    /// </summary>
    public Guid? CompanyId { get; init; }

    /// <summary>
    /// Additional context data (for custom filters)
    /// </summary>
    public Dictionary<string, object>? AdditionalData { get; init; }
}
