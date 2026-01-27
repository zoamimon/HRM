using System.Reflection;
using HRM.BuildingBlocks.Infrastructure.Persistence;
using HRM.Modules.Identity.Application.Abstractions.Data;
using HRM.Modules.Identity.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// DbContext for Identity module
/// Inherits from ModuleDbContext for:
/// - Unit of Work pattern
/// - Domain event dispatching
/// - Soft delete query filters
/// - Audit trail (CreatedAtUtc, ModifiedAtUtc, CreatedById, ModifiedById)
/// - Outbox pattern (OutboxMessages table)
///
/// Implements IIdentityQueryContext for:
/// - Dependency Inversion (Application layer depends on abstraction)
/// - Query handlers can access DbSets without referencing Infrastructure
///
/// Tables:
/// - Identity.Operators: Operator accounts
/// - Identity.OutboxMessages: Integration events for reliable publishing
///
/// Schema Separation:
/// - Schema: "Identity" (isolates from other modules)
/// - Same database: HrmDb (shared with Personnel, Payroll, etc.)
/// - Connection string: "HrmDb" from appsettings.json
///
/// Configuration:
/// - Entity configurations via IEntityTypeConfiguration
/// - Applied from assembly (OperatorConfiguration, etc.)
/// - Conventions: Snake_case column names, UTC datetime columns
///
/// Migrations:
/// - NOT USED (user requested database scripts instead)
/// - See: src/Database/Identity/*.sql
///
/// Performance:
/// - Indexes defined in entity configurations
/// - Query filters cached (executed once per model)
/// - Connection pooling enabled by default
///
/// Module Name:
/// - MUST override ModuleName property
/// - Used for distributed locking in OutboxProcessor
/// - Format: "Identity" (matches schema name)
/// </summary>
public sealed class IdentityDbContext : ModuleDbContext, IIdentityQueryContext
{
    public IdentityDbContext(
        DbContextOptions<IdentityDbContext> options,
        IPublisher publisher)
        : base(options, publisher)
    {
    }

    /// <summary>
    /// Module name for distributed locking
    /// CRITICAL: Must be unique across all modules
    /// Used by OutboxProcessor for SQL Server application locks
    /// </summary>
    public override string ModuleName => "Identity";

    /// <summary>
    /// Operators table
    /// Contains operator accounts (username, email, password, etc.)
    /// </summary>
    public DbSet<Operator> Operators => Set<Operator>();

    /// <summary>
    /// Refresh tokens table
    /// Contains refresh tokens for JWT authentication and session management
    /// Enables multi-device sessions, token revocation, and security audit trail
    /// </summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>
    /// Configure entity mappings
    /// Applies all IEntityTypeConfiguration from current assembly
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply base configuration (soft delete filters, outbox, etc.)
        base.OnModelCreating(modelBuilder);

        // Set default schema for Identity module
        modelBuilder.HasDefaultSchema("Identity");

        // Apply entity configurations from assembly
        // Automatically discovers: OperatorConfiguration, etc.
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
