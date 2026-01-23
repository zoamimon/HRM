using HRM.BuildingBlocks.Domain.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.BuildingBlocks.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework Core configuration for OutboxMessage entity
///
/// Configures:
/// - Table mapping
/// - Primary key
/// - Column constraints (length, required, etc.)
/// - Indexes for query optimization
///
/// Note: This configuration is already applied in ModuleDbContext.OnModelCreating()
/// This separate configuration file is provided for clarity and can be used
/// if modules want to override the default configuration
/// </summary>
public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        // Primary key
        builder.HasKey(e => e.Id);

        // Type - Full type name of integration event
        builder.Property(e => e.Type)
            .IsRequired()
            .HasMaxLength(500)
            .HasComment("Full type name of the integration event");

        // Content - Serialized JSON
        builder.Property(e => e.Content)
            .IsRequired()
            .HasComment("Serialized JSON content of the integration event");

        // OccurredOnUtc - When the domain event occurred
        builder.Property(e => e.OccurredOnUtc)
            .IsRequired()
            .HasComment("When the domain event occurred (NOT when outbox message created)");

        // ProcessedOnUtc - NULL if not processed yet
        builder.Property(e => e.ProcessedOnUtc)
            .HasComment("When the message was successfully processed (NULL = pending)");

        // Error - Error message if processing failed
        builder.Property(e => e.Error)
            .HasMaxLength(2000)
            .HasComment("Error message if processing failed");

        // AttemptCount - Number of retry attempts
        builder.Property(e => e.AttemptCount)
            .IsRequired()
            .HasDefaultValue(0)
            .HasComment("Number of processing attempts");

        // Indexes for query optimization

        // Index for finding unprocessed messages
        // Query: WHERE ProcessedOnUtc IS NULL
        // Comment: Optimizes queries for unprocessed messages
        builder.HasIndex(e => e.ProcessedOnUtc)
            .HasDatabaseName("IX_OutboxMessages_ProcessedOnUtc")
            .HasFilter("[ProcessedOnUtc] IS NULL");

        // Index for ordering by occurrence time
        // Query: ORDER BY OccurredOnUtc
        // Comment: Optimizes ordering by event occurrence time
        builder.HasIndex(e => e.OccurredOnUtc)
            .HasDatabaseName("IX_OutboxMessages_OccurredOnUtc");

        // Composite index for finding retryable messages
        // Query: WHERE ProcessedOnUtc IS NULL AND AttemptCount < @MaxAttempts ORDER BY OccurredOnUtc
        // Comment: Optimizes queries for finding and ordering retryable messages
        builder.HasIndex(e => new { e.ProcessedOnUtc, e.AttemptCount, e.OccurredOnUtc })
            .HasDatabaseName("IX_OutboxMessages_Processing")
            .HasFilter("[ProcessedOnUtc] IS NULL");

        // Audit fields from Entity base class
        builder.Property(e => e.CreatedAtUtc)
            .IsRequired()
            .HasComment("When the outbox message was created");

        builder.Property(e => e.ModifiedAtUtc)
            .HasComment("When the outbox message was last modified");
    }
}
