using HRM.BuildingBlocks.Infrastructure.BackgroundServices;
using HRM.BuildingBlocks.Infrastructure.Persistence;
using HRM.Modules.Identity.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HRM.Modules.Identity.Infrastructure.BackgroundServices;

/// <summary>
/// Background service for processing Identity module's outbox messages
/// Inherits from OutboxProcessor base class
///
/// Responsibilities:
/// - Poll Identity.OutboxMessages table every 60 seconds
/// - Deserialize and publish integration events
/// - Mark messages as processed or failed
/// - Implement retry logic with exponential backoff
/// - Use distributed locking for scaled deployments
///
/// Integration Events Published:
/// - OperatorRegisteredIntegrationEvent: When operator registered
/// - OperatorActivatedIntegrationEvent: When operator activated (if implemented)
/// - Other operator lifecycle events (future)
///
/// Distributed Locking:
/// - Lock resource: "OutboxProcessor_Identity"
/// - Ensures only ONE instance processes outbox at a time
/// - Critical for scaled deployments (multiple API instances)
/// - Uses SQL Server sp_getapplock (no external dependencies)
///
/// Configuration:
/// - Polling interval: 60 seconds (configurable in constructor)
/// - Batch size: 100 messages per iteration (configurable)
/// - Max attempts: 5 retries before dead letter (configurable)
///
/// Error Handling:
/// - Transient errors: Retry with incremented attempt count
/// - Permanent errors: Mark as failed, stop retrying after max attempts
/// - Dead letter messages: Require manual intervention
/// - All errors logged for monitoring
///
/// Performance:
/// - Processes up to 100 messages per iteration
/// - Chronological order (OccurredOnUtc ASC)
/// - Efficient queries with WHERE ProcessedOnUtc IS NULL
/// - Index on ProcessedOnUtc for fast filtering
///
/// Deployment:
/// - Registered as IHostedService in DI container
/// - Starts automatically with application
/// - Stops gracefully on application shutdown
/// - Safe for multiple instances (distributed locking)
///
/// Monitoring:
/// - Log level Information: Successful processing, message counts
/// - Log level Warning: Dead letter messages (max attempts reached)
/// - Log level Error: Processing failures, lock errors
/// - Metrics to track: Messages processed, failures, dead letters
/// </summary>
public sealed class IdentityOutboxProcessor : OutboxProcessor
{
    public IdentityOutboxProcessor(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<OutboxProcessor> logger)
        : base(
            serviceScopeFactory,
            logger,
            pollingInterval: TimeSpan.FromMinutes(1), // 60 seconds
            batchSize: 100,                           // 100 messages per iteration
            maxAttempts: 5)                           // 5 retries before dead letter
    {
    }

    /// <summary>
    /// Get IdentityDbContext from service provider
    /// Required by base class for database operations
    /// </summary>
    /// <param name="serviceProvider">Service provider from current scope</param>
    /// <returns>IdentityDbContext (derived from ModuleDbContext)</returns>
    protected override ModuleDbContext GetDbContext(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<IdentityDbContext>();
    }
}
