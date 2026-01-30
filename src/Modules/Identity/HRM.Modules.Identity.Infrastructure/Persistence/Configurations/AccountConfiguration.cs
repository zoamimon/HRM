using HRM.Modules.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Modules.Identity.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core entity configuration for Account aggregate.
///
/// Table: Identity.Accounts
/// Primary Key: Id (GUID)
/// Unique Constraints: Username, Email
/// </summary>
internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");

        builder.HasKey(a => a.Id);

        // Username: unique, case-insensitive
        builder.Property(a => a.Username)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnType("NVARCHAR(50)");

        // Email: unique, case-insensitive
        builder.Property(a => a.Email)
            .IsRequired()
            .HasMaxLength(255)
            .HasColumnType("NVARCHAR(255)");

        // PasswordHash: BCrypt/Argon2 hash
        builder.Property(a => a.PasswordHash)
            .IsRequired()
            .HasMaxLength(255)
            .HasColumnType("NVARCHAR(255)");

        // AccountType: enum as int
        builder.Property(a => a.AccountType)
            .IsRequired()
            .HasConversion<int>();

        // Status: enum as int
        builder.Property(a => a.Status)
            .IsRequired()
            .HasConversion<int>();

        // FullName
        builder.Property(a => a.FullName)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnType("NVARCHAR(200)");

        // PhoneNumber: optional
        builder.Property(a => a.PhoneNumber)
            .HasMaxLength(20)
            .HasColumnType("NVARCHAR(20)");

        // 2FA
        builder.Property(a => a.IsTwoFactorEnabled)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(a => a.TwoFactorSecretKey)
            .HasMaxLength(255)
            .HasColumnType("NVARCHAR(255)");

        // Timestamps
        builder.Property(a => a.ActivatedAtUtc).HasColumnType("DATETIME2");
        builder.Property(a => a.LastLoginAtUtc).HasColumnType("DATETIME2");
        builder.Property(a => a.LockedUntilUtc).HasColumnType("DATETIME2");

        // Security audit timestamps
        builder.Property(a => a.PasswordChangedAtUtc).HasColumnType("DATETIME2");
        builder.Property(a => a.LastFailedLoginAtUtc).HasColumnType("DATETIME2");
        builder.Property(a => a.TwoFactorChangedAtUtc).HasColumnType("DATETIME2");
        builder.Property(a => a.StatusChangedAtUtc).HasColumnType("DATETIME2");

        // Lockout
        builder.Property(a => a.FailedLoginAttempts)
            .IsRequired()
            .HasDefaultValue(0);

        // Unique indexes
        builder.HasIndex(a => a.Username)
            .IsUnique()
            .HasDatabaseName("IX_Accounts_Username");

        builder.HasIndex(a => a.Email)
            .IsUnique()
            .HasDatabaseName("IX_Accounts_Email");

        // Non-unique indexes
        builder.HasIndex(a => a.Status)
            .HasDatabaseName("IX_Accounts_Status");

        builder.HasIndex(a => a.AccountType)
            .HasDatabaseName("IX_Accounts_AccountType");

        builder.HasIndex(a => a.CreatedAtUtc)
            .HasDatabaseName("IX_Accounts_CreatedAtUtc");
    }
}
