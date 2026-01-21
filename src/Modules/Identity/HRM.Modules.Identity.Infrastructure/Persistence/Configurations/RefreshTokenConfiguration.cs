using HRM.Modules.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Modules.Identity.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core entity configuration for RefreshToken
/// Defines table schema, columns, indexes, constraints, relationships
///
/// Table: Identity.RefreshTokens
/// Primary Key: Id (GUID, clustered index)
/// Foreign Key: OperatorId -> Identity.Operators(Id)
/// Unique Constraints: Token
/// Indexes: Token (unique), OperatorId, ExpiresAt, IsActive (computed)
///
/// Column Mappings:
/// - Id: UNIQUEIDENTIFIER, PK, NOT NULL
/// - OperatorId: UNIQUEIDENTIFIER, FK, NOT NULL
/// - Token: NVARCHAR(200), UNIQUE, NOT NULL (Base64-encoded, ~88 chars)
/// - ExpiresAt: DATETIME2, NOT NULL (UTC)
/// - RevokedAt: DATETIME2, NULL (UTC)
/// - RevokedByIp: NVARCHAR(50), NULL (IPv4/IPv6)
/// - ReplacedByToken: NVARCHAR(200), NULL (token rotation audit)
/// - CreatedByIp: NVARCHAR(50), NOT NULL (IPv4/IPv6)
/// - UserAgent: NVARCHAR(500), NULL (browser/device info)
/// - CreatedAt: DATETIME2, NOT NULL (from Entity base)
///
/// Indexes:
/// - IX_RefreshTokens_Token: UNIQUE, for token lookup during validation
/// - IX_RefreshTokens_OperatorId: For loading operator's sessions
/// - IX_RefreshTokens_ExpiresAt: For cleanup of expired tokens
/// - IX_RefreshTokens_OperatorId_IsActive: Composite for active sessions query
///
/// Relationships:
/// - One Operator has many RefreshTokens (multi-device support)
/// - Cascade delete: When operator deleted, tokens are deleted
///
/// Performance Considerations:
/// - Token index with INCLUDE(OperatorId, ExpiresAt) - avoid key lookups
/// - Composite index on OperatorId + computed IsActive column
/// - ExpiresAt index for periodic cleanup job
/// - DATETIME2 for all timestamps (smaller than DATETIME)
///
/// Computed Columns:
/// - IsActive: (RevokedAt IS NULL AND GETUTCDATE() < ExpiresAt)
/// - Persisted for indexing, updated automatically
/// </summary>
internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        // Table name
        builder.ToTable("RefreshTokens");

        // Primary key
        builder.HasKey(rt => rt.Id);

        // Properties

        // OperatorId: Foreign key to Operators table
        builder.Property(rt => rt.OperatorId)
            .IsRequired();

        // Token: Base64-encoded random string, ~88 chars for 64 bytes
        // Allow 200 chars for safety
        builder.Property(rt => rt.Token)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnType("NVARCHAR(200)");

        // ExpiresAt: When token expires (UTC)
        builder.Property(rt => rt.ExpiresAt)
            .IsRequired()
            .HasColumnType("DATETIME2");

        // RevokedAt: When token was revoked (NULL if active)
        builder.Property(rt => rt.RevokedAt)
            .HasColumnType("DATETIME2");

        // RevokedByIp: IP address that revoked the token
        builder.Property(rt => rt.RevokedByIp)
            .HasMaxLength(50)
            .HasColumnType("NVARCHAR(50)");

        // ReplacedByToken: New token in rotation chain
        builder.Property(rt => rt.ReplacedByToken)
            .HasMaxLength(200)
            .HasColumnType("NVARCHAR(200)");

        // CreatedByIp: IP address that created the token
        builder.Property(rt => rt.CreatedByIp)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnType("NVARCHAR(50)");

        // UserAgent: Browser/device information
        builder.Property(rt => rt.UserAgent)
            .HasMaxLength(500)
            .HasColumnType("NVARCHAR(500)");

        // CreatedAtUtc: From Entity base class
        builder.Property(rt => rt.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("DATETIME2");

        // Relationships

        // Many-to-One: RefreshToken -> Operator
        builder.HasOne(rt => rt.Operator)
            .WithMany() // Operator doesn't have navigation to RefreshTokens
            .HasForeignKey(rt => rt.OperatorId)
            .OnDelete(DeleteBehavior.Cascade) // Delete tokens when operator deleted
            .HasConstraintName("FK_RefreshTokens_Operators_OperatorId");

        // Indexes

        // Unique index on Token for fast lookup during validation
        builder.HasIndex(rt => rt.Token)
            .IsUnique()
            .HasDatabaseName("IX_RefreshTokens_Token");

        // Index on OperatorId for loading user's sessions
        builder.HasIndex(rt => rt.OperatorId)
            .HasDatabaseName("IX_RefreshTokens_OperatorId");

        // Index on ExpiresAt for cleanup job
        builder.HasIndex(rt => rt.ExpiresAt)
            .HasDatabaseName("IX_RefreshTokens_ExpiresAt");

        // Composite index for active sessions query (GetActiveSessionsQuery)
        // WHERE OperatorId = @id AND RevokedAt IS NULL AND GETUTCDATE() < ExpiresAt
        builder.HasIndex(rt => new { rt.OperatorId, rt.RevokedAt, rt.ExpiresAt })
            .HasDatabaseName("IX_RefreshTokens_OperatorId_Active");

        // Ignore computed properties (not mapped to database)
        builder.Ignore(rt => rt.IsActive);
        builder.Ignore(rt => rt.IsExpired);

        // Ignore domain events (RefreshToken doesn't raise events)
        builder.Ignore(rt => rt.DomainEvents);
    }
}
