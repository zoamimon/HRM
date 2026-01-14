using HRM.BuildingBlocks.Domain.Enums;

namespace HRM.BuildingBlocks.Application.Abstractions.Data;

/// <summary>
/// Service for applying data scoping filters based on user's scope level.
/// Ensures users only access data within their authorized scope.
/// 
/// Scope Levels:
/// 1. Operator (Global Access):
///    - No filtering applied
///    - Can see all data across all companies/departments/positions
///    - System administrators
/// 
/// 2. Company Level:
///    - Can see all data within assigned companies
///    - Includes all departments and positions in those companies
///    - Example: CEO, General Manager
/// 
/// 3. Department Level:
///    - Can see data within assigned departments
///    - Includes all positions in those departments
///    - Example: Department Manager, HR Manager
/// 
/// 4. Position Level:
///    - Can see data for employees with same position
///    - Team-level access
///    - Example: Team Lead, Senior Developer
/// 
/// 5. Employee Level:
///    - Can only see own data
///    - Self-service access only
///    - Example: Regular Employee
/// 
/// Data Scoping Implementation:
/// <code>
/// public class SearchEmployeesQueryHandler 
///     : IQueryHandler&lt;SearchEmployeesQuery, List&lt;EmployeeDto&gt;&gt;
/// {
///     private readonly IDbConnection _connection;
///     private readonly IDataScopingService _scopingService;
///     
///     public async Task&lt;List&lt;EmployeeDto&gt;&gt; Handle(
///         SearchEmployeesQuery query,
///         CancellationToken cancellationToken)
///     {
///         // 1. Get current user's scope context
///         var scopeContext = await _scopingService.GetCurrentScopeAsync(cancellationToken);
///         
///         // 2. Build base SQL
///         var sql = @"
///             SELECT DISTINCT 
///                 e.Id, e.FirstName, e.LastName, e.Email,
///                 d.Name AS DepartmentName,
///                 p.Name AS PositionName
///             FROM personnel.Employees e
///             INNER JOIN personnel.EmployeeAssignments ea ON e.Id = ea.EmployeeId
///             LEFT JOIN organization.Departments d ON ea.DepartmentId = d.Id
///             LEFT JOIN organization.Positions p ON ea.PositionId = p.Id
///             WHERE ea.EndDate IS NULL
///                 {0}  -- Scope filter injected here
///             ORDER BY e.LastName, e.FirstName";
///         
///         var parameters = new DynamicParameters();
///         
///         // 3. Apply data scoping
///         string scopeFilter = _scopingService.BuildScopeFilter(
///             scopeContext,
///             parameters
///         );
///         
///         sql = string.Format(sql, scopeFilter);
///         
///         // 4. Execute query with scoping applied
///         var employees = await _connection.QueryAsync&lt;EmployeeDto&gt;(sql, parameters);
///         
///         return employees.ToList();
///     }
/// }
/// </code>
/// 
/// Scope Filter Examples:
/// 
/// Operator (No Filter):
/// <code>
/// // No WHERE clause added
/// // User sees all employees
/// </code>
/// 
/// Company Level:
/// <code>
/// AND ea.CompanyId IN @AllowedCompanyIds
/// // Parameters: AllowedCompanyIds = [CompanyA, CompanyB]
/// </code>
/// 
/// Department Level:
/// <code>
/// AND ea.DepartmentId IN @AllowedDepartmentIds
/// // Parameters: AllowedDepartmentIds = [Dept1, Dept2, Dept3]
/// </code>
/// 
/// Position Level:
/// <code>
/// AND ea.PositionId IN @AllowedPositionIds
/// // Parameters: AllowedPositionIds = [SeniorDev, TechLead]
/// </code>
/// 
/// Employee Level:
/// <code>
/// AND e.Id = @CurrentUserId
/// // Parameters: CurrentUserId = {user's employee ID}
/// </code>
/// 
/// Scope Context Loading:
/// Service queries active assignments to determine allowed IDs:
/// <code>
/// SELECT DISTINCT ea.CompanyId
/// FROM personnel.EmployeeAssignments ea
/// WHERE ea.EmployeeId = @UserId
///     AND (ea.EndDate IS NULL OR ea.EndDate > GETUTCDATE())
/// </code>
/// 
/// Caching Strategy:
/// - Scope context loaded once per request
/// - Cached in request scope (scoped service lifetime)
/// - No database query for subsequent calls in same request
/// - Invalidated at end of request
/// 
/// Security Considerations:
/// 1. Always apply scoping for User requests
/// 2. Operators bypass scoping (global access)
/// 3. Validate scope level matches user's actual assignments
/// 4. No client-side scope filtering (must be server-side)
/// 5. Scope violations return empty results (not errors)
/// 
/// Performance Optimization:
/// 1. Use indexed columns for filtering (CompanyId, DepartmentId, PositionId)
/// 2. Apply scoping as early as possible in query
/// 3. Use Dapper for complex queries with scoping
/// 4. Cache scope context per request
/// 5. Use DISTINCT only when necessary
/// 
/// Testing:
/// <code>
/// [Fact]
/// public async Task GetCurrentScopeAsync_WhenDepartmentLevelUser_ShouldReturnDepartmentIds()
/// {
///     // Arrange
///     var userId = Guid.NewGuid();
///     var scopeService = CreateScopeService(userId, ScopeLevel.Department);
///     
///     // Act
///     var scopeContext = await scopeService.GetCurrentScopeAsync();
///     
///     // Assert
///     scopeContext.ScopeLevel.Should().Be(ScopeLevel.Department);
///     scopeContext.AllowedDepartmentIds.Should().NotBeEmpty();
///     scopeContext.AllowedCompanyIds.Should().NotBeEmpty();
/// }
/// </code>
/// 
/// Common Patterns:
/// 
/// 1. Query with Scoping:
/// <code>
/// var scopeContext = await _scopingService.GetCurrentScopeAsync();
/// var filter = _scopingService.BuildScopeFilter(scopeContext, parameters);
/// var sql = $"SELECT * FROM Employees WHERE Active = 1 {filter}";
/// </code>
/// 
/// 2. Check if Operator:
/// <code>
/// var scopeContext = await _scopingService.GetCurrentScopeAsync();
/// if (scopeContext.UserType == UserType.Operator)
/// {
///     // No scoping needed
///     return await QueryAllDataAsync();
/// }
/// </code>
/// 
/// 3. Validate Entity Access:
/// <code>
/// var scopeContext = await _scopingService.GetCurrentScopeAsync();
/// if (!scopeContext.CanAccessEmployee(employeeId))
/// {
///     return Result.Failure(Error.Forbidden(...));
/// }
/// </code>
/// </summary>
public interface IDataScopingService
{
    /// <summary>
    /// Gets the current user's data scope context.
    /// Loads active assignments and determines allowed IDs for filtering.
    /// 
    /// Returned Context Contains:
    /// - UserType: Operator or User
    /// - UserId: Current user identifier
    /// - ScopeLevel: User's scope level (null for Operators)
    /// - AllowedCompanyIds: Companies user can access
    /// - AllowedDepartmentIds: Departments user can access
    /// - AllowedPositionIds: Positions user can access
    /// 
    /// Loading Strategy:
    /// 1. Extract user info from JWT claims (ICurrentUserService)
    /// 2. If Operator → return context with no restrictions
    /// 3. If User → query EmployeeAssignments for active assignments
    /// 4. Build allowed IDs based on ScopeLevel
    /// 5. Cache context for request lifetime
    /// 
    /// Performance:
    /// - Cached per request (scoped service)
    /// - Single database query for assignments
    /// - Reused across multiple query handlers
    /// 
    /// Example:
    /// <code>
    /// var scopeContext = await _scopingService.GetCurrentScopeAsync(cancellationToken);
    /// 
    /// // For Operator:
    /// // - UserType = Operator
    /// // - ScopeLevel = null
    /// // - AllowedCompanyIds = empty (no filtering)
    /// 
    /// // For Department-Level User:
    /// // - UserType = User
    /// // - ScopeLevel = Department
    /// // - AllowedDepartmentIds = [Dept1, Dept2]
    /// // - AllowedCompanyIds = [CompanyA] (parent companies)
    /// </code>
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Scope context containing user type and allowed IDs</returns>
    Task<DataScopeContext> GetCurrentScopeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds SQL WHERE clause filter based on scope context.
    /// Adds appropriate filtering for user's scope level.
    /// 
    /// Filter Generation:
    /// - Operator: Returns empty string (no filter)
    /// - Company: "AND ea.CompanyId IN @AllowedCompanyIds"
    /// - Department: "AND ea.DepartmentId IN @AllowedDepartmentIds"
    /// - Position: "AND ea.PositionId IN @AllowedPositionIds"
    /// - Employee: "AND e.Id = @CurrentUserId"
    /// 
    /// Parameters:
    /// Populates Dapper DynamicParameters with filter values:
    /// - @AllowedCompanyIds: List&lt;Guid&gt;
    /// - @AllowedDepartmentIds: List&lt;Guid&gt;
    /// - @AllowedPositionIds: List&lt;Guid&gt;
    /// - @CurrentUserId: Guid
    /// 
    /// Usage:
    /// <code>
    /// var scopeContext = await _scopingService.GetCurrentScopeAsync();
    /// var parameters = new DynamicParameters();
    /// 
    /// var sql = @"
    ///     SELECT * FROM Employees e
    ///     INNER JOIN EmployeeAssignments ea ON e.Id = ea.EmployeeId
    ///     WHERE ea.EndDate IS NULL
    ///         {0}"; // Scope filter injected here
    /// 
    /// string filter = _scopingService.BuildScopeFilter(scopeContext, parameters);
    /// sql = string.Format(sql, filter);
    /// 
    /// var result = await _connection.QueryAsync&lt;EmployeeDto&gt;(sql, parameters);
    /// </code>
    /// 
    /// Table Alias Requirements:
    /// - Assumes 'ea' alias for EmployeeAssignments table
    /// - Assumes 'e' alias for Employees table
    /// - Adjust aliases in your SQL if different
    /// </summary>
    /// <param name="scopeContext">Scope context from GetCurrentScopeAsync</param>
    /// <param name="parameters">Dapper parameters object to populate</param>
    /// <returns>SQL WHERE clause filter (including AND keyword)</returns>
    string BuildScopeFilter(DataScopeContext scopeContext, dynamic parameters);

    /// <summary>
    /// Checks if current user can access a specific employee's data.
    /// Validates employee ID against user's scope.
    /// 
    /// Validation Rules:
    /// - Operator: Can access all employees (return true)
    /// - Company Level: Check if employee in allowed companies
    /// - Department Level: Check if employee in allowed departments
    /// - Position Level: Check if employee has allowed position
    /// - Employee Level: Check if employeeId == current user's ID
    /// 
    /// Usage:
    /// <code>
    /// public async Task&lt;Result&gt; Handle(UpdateEmployeeCommand command, ...)
    /// {
    ///     var scopeContext = await _scopingService.GetCurrentScopeAsync();
    ///     
    ///     if (!await _scopingService.CanAccessEmployeeAsync(
    ///         scopeContext, 
    ///         command.EmployeeId))
    ///     {
    ///         return Result.Failure(
    ///             Error.Forbidden(
    ///                 "Employee.AccessDenied",
    ///                 "You don't have permission to access this employee"
    ///             )
    ///         );
    ///     }
    ///     
    ///     // Proceed with update
    /// }
    /// </code>
    /// 
    /// Performance:
    /// - For Operators: No database query (immediate true)
    /// - For Users: Single query to check employee assignments
    /// - Consider caching if called multiple times per request
    /// </summary>
    /// <param name="scopeContext">Scope context from GetCurrentScopeAsync</param>
    /// <param name="employeeId">Employee ID to check access for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if user can access employee, false otherwise</returns>
    Task<bool> CanAccessEmployeeAsync(
        DataScopeContext scopeContext,
        Guid employeeId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Contains data scope information for the current user.
/// Used to filter queries based on user's authorized data access.
/// </summary>
public sealed class DataScopeContext
{
    /// <summary>
    /// Type of user (Operator or User).
    /// Operators have global access, Users have scoped access.
    /// </summary>
    public required UserType UserType { get; init; }

    /// <summary>
    /// Current user's identifier.
    /// For Operators: OperatorId
    /// For Users: UserId (same as EmployeeId)
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// User's scope level (null for Operators).
    /// Determines granularity of data access.
    /// </summary>
    public ScopeLevel? ScopeLevel { get; init; }

    /// <summary>
    /// List of company IDs user can access.
    /// Empty for Operators (access all companies).
    /// Populated for Users based on active assignments.
    /// </summary>
    public List<Guid> AllowedCompanyIds { get; init; } = new();

    /// <summary>
    /// List of department IDs user can access.
    /// Empty for Operators and Company-level users.
    /// Populated for Department/Position/Employee level users.
    /// </summary>
    public List<Guid> AllowedDepartmentIds { get; init; } = new();

    /// <summary>
    /// List of position IDs user can access.
    /// Empty for Operators and Company/Department-level users.
    /// Populated for Position-level users.
    /// </summary>
    public List<Guid> AllowedPositionIds { get; init; } = new();

    /// <summary>
    /// Checks if user is an Operator (global access).
    /// </summary>
    public bool IsOperator => UserType == UserType.Operator;

    /// <summary>
    /// Checks if user is a regular User (scoped access).
    /// </summary>
    public bool IsUser => UserType == UserType.User;

    /// <summary>
    /// Checks if any scoping should be applied.
    /// False for Operators, true for Users.
    /// </summary>
    public bool RequiresScoping => UserType == UserType.User;
}
