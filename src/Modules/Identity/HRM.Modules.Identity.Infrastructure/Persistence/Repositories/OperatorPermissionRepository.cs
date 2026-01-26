using System.Data;
using Dapper;
using HRM.Modules.Identity.Domain.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace HRM.Modules.Identity.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementation of IOperatorPermissionRepository using Dapper
///
/// Design:
/// - Uses Dapper for optimized read-only queries
/// - Direct SQL for performance (bypasses EF Core overhead)
/// - Connection per query (no context dependency)
///
/// Queries:
/// - GetPermissionsAsync: Joins Operator -> OperatorRoles -> Roles -> RolePermissions
/// - IsSuperAdminAsync: Checks for "System Administrator" role
/// - HasPermissionAsync: Optimized single permission check
/// </summary>
public sealed class OperatorPermissionRepository : IOperatorPermissionRepository
{
    private readonly string _connectionString;

    private const string SystemAdminRoleName = "System Administrator";

    public OperatorPermissionRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("HrmDatabase")
            ?? throw new InvalidOperationException("Connection string 'HrmDatabase' not found");
    }

    /// <summary>
    /// Get all permission keys for an operator
    /// </summary>
    public async Task<HashSet<string>> GetPermissionsAsync(
        Guid operatorId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT DISTINCT
                rp.Module + '.' + rp.Entity + '.' + rp.Action AS PermissionKey
            FROM [Identity].Operators o
            INNER JOIN [Identity].OperatorRoles opr ON o.Id = opr.OperatorId
            INNER JOIN [Identity].Roles r ON opr.RoleId = r.Id
            INNER JOIN [Identity].RolePermissions rp ON r.Id = rp.RoleId
            WHERE o.Id = @OperatorId
              AND o.IsDeleted = 0
              AND o.Status = 1  -- Active status
              AND r.IsDeleted = 0
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var permissions = await connection.QueryAsync<string>(
            new CommandDefinition(
                sql,
                new { OperatorId = operatorId },
                cancellationToken: cancellationToken));

        return permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Check if operator has "System Administrator" role
    /// </summary>
    public async Task<bool> IsSuperAdminAsync(
        Guid operatorId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM [Identity].Operators o
                INNER JOIN [Identity].OperatorRoles opr ON o.Id = opr.OperatorId
                INNER JOIN [Identity].Roles r ON opr.RoleId = r.Id
                WHERE o.Id = @OperatorId
                  AND o.IsDeleted = 0
                  AND o.Status = 1  -- Active status
                  AND r.IsDeleted = 0
                  AND r.Name = @SystemAdminRoleName
            ) THEN 1 ELSE 0 END
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var result = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                sql,
                new { OperatorId = operatorId, SystemAdminRoleName },
                cancellationToken: cancellationToken));

        return result == 1;
    }

    /// <summary>
    /// Check if operator has specific permission
    /// </summary>
    public async Task<bool> HasPermissionAsync(
        Guid operatorId,
        string module,
        string entity,
        string action,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM [Identity].Operators o
                INNER JOIN [Identity].OperatorRoles opr ON o.Id = opr.OperatorId
                INNER JOIN [Identity].Roles r ON opr.RoleId = r.Id
                INNER JOIN [Identity].RolePermissions rp ON r.Id = rp.RoleId
                WHERE o.Id = @OperatorId
                  AND o.IsDeleted = 0
                  AND o.Status = 1  -- Active status
                  AND r.IsDeleted = 0
                  AND rp.Module = @Module
                  AND rp.Entity = @Entity
                  AND rp.Action = @Action
            ) THEN 1 ELSE 0 END
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var result = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                sql,
                new { OperatorId = operatorId, Module = module, Entity = entity, Action = action },
                cancellationToken: cancellationToken));

        return result == 1;
    }
}
