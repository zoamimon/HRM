using HRM.Modules.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Modules.Identity.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core entity configuration for RefreshToken
/// Defines table schema, columns, indexes, constraints
///
/// Table: Identity.RefreshTokens
/// Primary Key: Id (GUID, clustered index)
/// Polymorphic Design: UserType + PrincipalId (no FK constraint)
/// Unique Constraints: Token
/// Indexes: Token (unique), (UserType, PrincipalId), ExpiresAt
///
/// Column Mappings:
/// - Id: UNIQUEIDENTIFIER, PK, NOT NULL
/// - UserType: TINYINT, NOT NULL (discriminator for polymorphic association)
/// - PrincipalId: UNIQUEIDENTIFIER, NOT NULL (polymorphic FK to Operators/Users)
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
/// - IX_RefreshTokens_UserType_PrincipalId: For loading user's sessions
/// - IX_RefreshTokens_ExpiresAt: For cleanup of expired tokens
/// - IX_RefreshTokens_UserType_PrincipalId_Active: Composite for active sessions query
///
/// Polymorphic Relationships:
/// - UserType.Operator + PrincipalId → Identity.Operators.Id
/// - UserType.User + PrincipalId → Identity.User.Id
/// - No FK constraint (polymorphic limitation)
/// - Application validates principal exists before creating token
///
/// Performance Considerations:
/// - Token index with INCLUDE(UserType, PrincipalId, ExpiresAt) - avoid key lookups
/// - Composite index on (UserType, PrincipalId, RevokedAt, ExpiresAt)
/// - ExpiresAt index for periodic cleanup job
/// - DATETIME2 for all timestamps (smaller than DATETIME)
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

        // UserType: Discriminator for polymorphic association
        builder.Property(rt => rt.UserType)
            .IsRequired()
            .HasColumnType("TINYINT");

        // PrincipalId: Polymorphic foreign key (Operator.Id or User.Id)
        builder.Property(rt => rt.PrincipalId)
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
        // None - Polymorphic design uses UserType + PrincipalId
        // No FK constraint possible across multiple tables
        // Application must validate principal exists before creating token

        // Indexes

        // Unique index on Token for fast lookup during validation
        builder.HasIndex(rt => rt.Token)
            .IsUnique()
            .HasDatabaseName("IX_RefreshTokens_Token");

        // Index on (UserType, PrincipalId) for loading user's sessions
        builder.HasIndex(rt => new { rt.UserType, rt.PrincipalId })
            .HasDatabaseName("IX_RefreshTokens_UserType_PrincipalId");

        // Index on ExpiresAt for cleanup job
        builder.HasIndex(rt => rt.ExpiresAt)
            .HasDatabaseName("IX_RefreshTokens_ExpiresAt");

        // Composite index for active sessions query (GetActiveSessionsQuery)
        // WHERE UserType = @type AND PrincipalId = @id AND RevokedAt IS NULL AND GETUTCDATE() < ExpiresAt
        builder.HasIndex(rt => new { rt.UserType, rt.PrincipalId, rt.RevokedAt, rt.ExpiresAt })
            .HasDatabaseName("IX_RefreshTokens_UserType_PrincipalId_Active");

        // Ignore computed properties (not mapped to database)
        builder.Ignore(rt => rt.IsActive);
        builder.Ignore(rt => rt.IsExpired);

        // Ignore domain events (RefreshToken doesn't raise events)
        builder.Ignore(rt => rt.DomainEvents);
    }
}
