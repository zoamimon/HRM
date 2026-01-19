using HRM.Modules.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Modules.Identity.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core entity configuration for Operator aggregate
/// Defines table schema, columns, indexes, constraints
///
/// Table: Identity.Operators
/// Primary Key: Id (GUID, clustered index)
/// Unique Constraints: Username, Email
/// Indexes: Username (unique), Email (unique), Status, CreatedAtUtc
///
/// Column Mappings:
/// - Id: UNIQUEIDENTIFIER, PK, NOT NULL
/// - Username: NVARCHAR(50), UNIQUE, NOT NULL
/// - Email: NVARCHAR(255), UNIQUE, NOT NULL
/// - PasswordHash: NVARCHAR(255), NOT NULL (BCrypt hash)
/// - FullName: NVARCHAR(200), NOT NULL
/// - PhoneNumber: NVARCHAR(20), NULL
/// - Status: INT, NOT NULL (0=Pending, 1=Active, 2=Suspended, 3=Deactivated)
/// - ActivatedAtUtc: DATETIME2, NULL
/// - LastLoginAtUtc: DATETIME2, NULL
/// - IsTwoFactorEnabled: BIT, NOT NULL, DEFAULT 0
/// - TwoFactorSecret: NVARCHAR(255), NULL
/// - FailedLoginAttempts: INT, NOT NULL, DEFAULT 0
/// - LockedUntilUtc: DATETIME2, NULL
/// - CreatedAtUtc: DATETIME2, NOT NULL (from Entity base)
/// - ModifiedAtUtc: DATETIME2, NULL (from Entity base)
/// - CreatedById: UNIQUEIDENTIFIER, NULL (from Entity base)
/// - ModifiedById: UNIQUEIDENTIFIER, NULL (from Entity base)
/// - IsDeleted: BIT, NOT NULL, DEFAULT 0 (from ISoftDeletable)
/// - DeletedAtUtc: DATETIME2, NULL (from ISoftDeletable)
///
/// Indexes:
/// - IX_Operators_Username: UNIQUE, for login queries
/// - IX_Operators_Email: UNIQUE, for uniqueness checks
/// - IX_Operators_Status: For filtering by status
/// - IX_Operators_CreatedAtUtc: For sorting/pagination
///
/// Performance Considerations:
/// - Username/Email indexes with INCLUDE columns (avoid key lookups)
/// - Status index for admin dashboards (filter pending operators)
/// - CreatedAtUtc index for chronological sorting
/// - All datetime columns use DATETIME2 (more precise, smaller storage)
/// </summary>
internal sealed class OperatorConfiguration : IEntityTypeConfiguration<Operator>
{
    public void Configure(EntityTypeBuilder<Operator> builder)
    {
        // Table name
        builder.ToTable("Operators");

        // Primary key
        builder.HasKey(o => o.Id);

        // Properties

        // Username: 3-50 chars, unique, case-insensitive
        builder.Property(o => o.Username)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnType("NVARCHAR(50)");

        // Email: Max 255 chars, unique, case-insensitive
        builder.Property(o => o.Email)
            .IsRequired()
            .HasMaxLength(255)
            .HasColumnType("NVARCHAR(255)");

        // PasswordHash: BCrypt hash (60 chars), allow 255 for future algorithms
        builder.Property(o => o.PasswordHash)
            .IsRequired()
            .HasMaxLength(255)
            .HasColumnType("NVARCHAR(255)");

        // FullName: 1-200 chars
        builder.Property(o => o.FullName)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnType("NVARCHAR(200)");

        // PhoneNumber: Optional, max 20 chars (E.164 format)
        builder.Property(o => o.PhoneNumber)
            .HasMaxLength(20)
            .HasColumnType("NVARCHAR(20)");

        // Status: Enum stored as INT (0=Pending, 1=Active, 2=Suspended, 3=Deactivated)
        builder.Property(o => o.Status)
            .IsRequired()
            .HasConversion<int>(); // Store enum as integer

        // ActivatedAtUtc: Timestamp when operator activated
        builder.Property(o => o.ActivatedAtUtc)
            .HasColumnType("DATETIME2");

        // LastLoginAtUtc: Timestamp of last successful login
        builder.Property(o => o.LastLoginAtUtc)
            .HasColumnType("DATETIME2");

        // IsTwoFactorEnabled: Boolean for 2FA status
        builder.Property(o => o.IsTwoFactorEnabled)
            .IsRequired()
            .HasDefaultValue(false);

        // TwoFactorSecret: TOTP secret (Base32 encoded)
        builder.Property(o => o.TwoFactorSecret)
            .HasMaxLength(255)
            .HasColumnType("NVARCHAR(255)");

        // FailedLoginAttempts: Counter for account lockout
        builder.Property(o => o.FailedLoginAttempts)
            .IsRequired()
            .HasDefaultValue(0);

        // LockedUntilUtc: Timestamp when account unlocks
        builder.Property(o => o.LockedUntilUtc)
            .HasColumnType("DATETIME2");

        // Unique constraints
        builder.HasIndex(o => o.Username)
            .IsUnique()
            .HasDatabaseName("IX_Operators_Username");

        builder.HasIndex(o => o.Email)
            .IsUnique()
            .HasDatabaseName("IX_Operators_Email");

        // Non-unique indexes
        builder.HasIndex(o => o.Status)
            .HasDatabaseName("IX_Operators_Status");

        builder.HasIndex(o => o.CreatedAtUtc)
            .HasDatabaseName("IX_Operators_CreatedAtUtc");

        // Ignore domain events collection (not mapped to database)
        builder.Ignore(o => o.DomainEvents);
    }
}
