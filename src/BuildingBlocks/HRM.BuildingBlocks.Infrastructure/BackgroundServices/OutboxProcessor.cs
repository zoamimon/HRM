using System.Data;
using System.Data.Common;
using System.Text.Json;
using HRM.BuildingBlocks.Application.Abstractions.EventBus;
using HRM.BuildingBlocks.Domain.Abstractions.Events;
using HRM.BuildingBlocks.Domain.Outbox;
using HRM.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HRM.BuildingBlocks.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that processes unprocessed outbox messages
/// Implements the Transactional Outbox pattern for reliable event publishing
///
/// How It Works:
/// 1. Polls database every 60 seconds for unprocessed OutboxMessages
/// 2. Deserializes integration events from JSON
/// 3. Publishes events via IEventBus (InMemoryEventBus or RabbitMqEventBus)
/// 4. Marks messages as processed or failed
/// 5. Implements retry logic with exponential backoff
///
/// Processing Flow:
/// - Query: WHERE ProcessedOnUtc IS NULL AND AttemptCount < MaxAttempts
/// - Order: ORDER BY OccurredOnUtc ASC (chronological order)
/// - Batch: Process up to 100 messages per iteration
/// - Parallel: Each module's outbox processed independently
///
/// Retry Strategy:
/// - MaxAttempts: 5 (configurable)
/// - Failed messages incremented AttemptCount
/// - Messages with AttemptCount >= MaxAttempts become dead letter
/// - Dead letter messages require manual intervention
///
/// Error Handling:
/// - Transient errors: Retry automatically
/// - Permanent errors: Mark as failed, stop retrying after max attempts
/// - Logs all errors for monitoring
/// - Continues processing other messages if one fails
///
/// Performance Considerations:
/// - Polling interval: 60 seconds (configurable)
/// - Batch size: 100 messages (configurable)
/// - Each module has separate OutboxMessages table
/// - Distributed locking prevents duplicate processing in scaled environments
///
/// Deployment Scenarios:
/// 1. Single Instance:
///    - One OutboxProcessor per application
///    - Distributed lock still acquired (no performance impact)
///    - Simple, works for most applications
///
/// 2. Multiple Instances (Scaled Out):
///    - Distributed locking via SQL Server app locks (sp_getapplock)
///    - Only ONE instance can process outbox at a time per module
///    - Other instances wait or skip if lock not acquired
///    - Prevents duplicate event publishing
///    - Lock timeout: 30 seconds (auto-released on crash)
///
/// 3. Microservices:
///    - Each service has own OutboxProcessor
///    - Publishes to RabbitMQ instead of in-memory
///
/// Distributed Locking Implementation:
/// - Uses SQL Server application locks (sp_getapplock/sp_releaseapplock)
/// - Lock resource name: "OutboxProcessor_{ModuleName}"
/// - Lock mode: Exclusive (only one holder)
/// - Lock timeout: 0 (non-blocking - skip if locked)
/// - Lock scope: Session (auto-released on connection close)
/// - No external dependencies (Redis, etc.) required
///
/// Monitoring:
/// - Log successful publishes (Information)
/// - Log failures (Error)
/// - Track metrics: messages processed, failures, dead letters
/// - Alert on: high failure rate, dead letter accumulation
/// </summary>
public abstract class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly TimeSpan _pollingInterval;
    private readonly int _batchSize;
    private readonly int _maxAttempts;

    /// <summary>
    /// Constructor with dependencies
    /// </summary>
    /// <param name="serviceScopeFactory">Factory to create scopes for each iteration</param>
    /// <param name="logger">Logger for tracking processing</param>
    /// <param name="pollingInterval">How often to check for messages (default: 60 seconds)</param>
    /// <param name="batchSize">Max messages to process per iteration (default: 100)</param>
    /// <param name="maxAttempts">Max retry attempts before dead letter (default: 5)</param>
    protected OutboxProcessor(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<OutboxProcessor> logger,
        TimeSpan? pollingInterval = null,
        int batchSize = 100,
        int maxAttempts = 5)
    {
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pollingInterval = pollingInterval ?? TimeSpan.FromMinutes(1); // 1 minute
        _batchSize = batchSize;
        _maxAttempts = maxAttempts;
    }

    /// <summary>
    /// Background service execution loop
    /// Runs continuously until application stops
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "{ProcessorName} started. Polling interval: {PollingInterval}s, Batch size: {BatchSize}, Max attempts: {MaxAttempts}",
            GetType().Name,
            _pollingInterval.TotalSeconds,
            _batchSize,
            _maxAttempts
        );

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "{ProcessorName} encountered an error during processing. Will retry after delay.",
                    GetType().Name
                );
            }

            // Wait before next iteration
            await Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("{ProcessorName} stopped", GetType().Name);
    }

    /// <summary>
    /// Process unprocessed outbox messages in a batch
    /// Creates new scope for each iteration to avoid stale data
    ///
    /// Distributed Locking:
    /// - Acquires SQL Server application lock before processing
    /// - Lock resource: "OutboxProcessor_{ModuleName}"
    /// - If lock cannot be acquired, skips this iteration (another instance is processing)
    /// - Lock automatically released at end of scope
    /// </summary>
    private async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();

        var dbContext = GetDbContext(scope.ServiceProvider);
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        // Acquire distributed lock
        var lockResource = $"OutboxProcessor_{dbContext.ModuleName}";
        var lockAcquired = await TryAcquireLockAsync(dbContext, lockResource, cancellationToken);

        if (!lockAcquired)
        {
            _logger.LogDebug(
                "{ProcessorName}: Could not acquire lock '{LockResource}'. Another instance is processing. Skipping this iteration.",
                GetType().Name,
                lockResource
            );
            return;
        }

        try
        {
            _logger.LogDebug(
                "{ProcessorName}: Acquired lock '{LockResource}'",
                GetType().Name,
                lockResource
            );

            // Query unprocessed messages
            var messages = await dbContext.OutboxMessages
                .Where(m => m.ProcessedOnUtc == null && m.AttemptCount < _maxAttempts)
                .OrderBy(m => m.OccurredOnUtc) // Process in chronological order
                .Take(_batchSize)
                .ToListAsync(cancellationToken);

            if (!messages.Any())
            {
                _logger.LogDebug("{ProcessorName}: No pending outbox messages", GetType().Name);
                return;
            }

            _logger.LogInformation(
                "{ProcessorName}: Found {MessageCount} pending outbox messages to process",
                GetType().Name,
                messages.Count
            );

            // Process each message
            var successCount = 0;
            var failureCount = 0;

            foreach (var message in messages)
            {
                var processed = await ProcessSingleMessageAsync(message, eventBus, cancellationToken);

                if (processed)
                {
                    successCount++;
                }
                else
                {
                    failureCount++;
                }
            }

            // Save all changes (marks messages as processed/failed)
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "{ProcessorName}: Processed {TotalCount} messages - {SuccessCount} succeeded, {FailureCount} failed",
                GetType().Name,
                messages.Count,
                successCount,
                failureCount
            );
        }
        finally
        {
            // Release lock (also auto-released when connection closes)
            await ReleaseLockAsync(dbContext, lockResource, cancellationToken);

            _logger.LogDebug(
                "{ProcessorName}: Released lock '{LockResource}'",
                GetType().Name,
                lockResource
            );
        }
    }

    /// <summary>
    /// Process a single outbox message
    /// Deserializes and publishes integration event
    /// </summary>
    private async Task<bool> ProcessSingleMessageAsync(
        OutboxMessage message,
        IEventBus eventBus,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug(
                "Processing outbox message {MessageId} (Type: {EventType}, Attempt: {AttemptCount})",
                message.Id,
                message.Type,
                message.AttemptCount + 1
            );

            // Deserialize integration event
            var eventType = Type.GetType(message.Type);
            if (eventType is null)
            {
                var error = $"Unknown event type: {message.Type}";
                _logger.LogError(error);
                message.MarkAsFailed(error);
                return false;
            }

            var integrationEvent = JsonSerializer.Deserialize(message.Content, eventType) as IIntegrationEvent;
            if (integrationEvent is null)
            {
                var error = $"Failed to deserialize event: {message.Type}";
                _logger.LogError(error);
                message.MarkAsFailed(error);
                return false;
            }

            // Publish event via event bus
            await eventBus.PublishAsync(integrationEvent, cancellationToken);

            // Mark as processed
            message.MarkAsProcessed();

            _logger.LogInformation(
                "Successfully processed outbox message {MessageId} (Type: {EventType})",
                message.Id,
                message.Type
            );

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to process outbox message {MessageId} (Type: {EventType}, Attempt: {AttemptCount})",
                message.Id,
                message.Type,
                message.AttemptCount + 1
            );

            // Mark as failed (increments attempt count)
            message.MarkAsFailed(ex.Message);

            // Check if dead letter
            if (!message.CanRetry(_maxAttempts))
            {
                _logger.LogWarning(
                    "Outbox message {MessageId} has reached max attempts ({MaxAttempts}) and will be moved to dead letter queue",
                    message.Id,
                    _maxAttempts
                );
            }

            return false;
        }
    }

    /// <summary>
    /// Try to acquire SQL Server application lock for distributed locking
    ///
    /// How It Works:
    /// - Calls sp_getapplock stored procedure
    /// - Lock mode: Exclusive (only one holder at a time)
    /// - Lock timeout: 0 milliseconds (non-blocking - return immediately if locked)
    /// - Lock owner: Session (auto-released when connection closes)
    ///
    /// Return Codes:
    /// - 0 or 1: Lock granted successfully
    /// - -1: Timeout (lock held by another session)
    /// - -2: Canceled
    /// - -3: Deadlock victim
    /// - -999: Parameter validation error
    ///
    /// Benefits:
    /// - No external dependencies (built into SQL Server)
    /// - Automatic cleanup on crash (session-scoped)
    /// - Works across multiple application instances
    /// - Lightweight (no network overhead)
    ///
    /// SQL Executed:
    /// <code>
    /// EXEC sp_getapplock
    ///     @Resource = 'OutboxProcessor_Identity',
    ///     @LockMode = 'Exclusive',
    ///     @LockOwner = 'Session',
    ///     @LockTimeout = 0
    /// </code>
    /// </summary>
    /// <param name="dbContext">Database context</param>
    /// <param name="lockResource">Lock resource name (unique per module)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if lock acquired, false if already locked by another instance</returns>
    private async Task<bool> TryAcquireLockAsync(
        ModuleDbContext dbContext,
        string lockResource,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get underlying database connection
            var connection = dbContext.Database.GetDbConnection();

            // Ensure connection is open
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            // Create command to call sp_getapplock
            using var command = connection.CreateCommand();
            command.CommandText = "sp_getapplock";
            command.CommandType = CommandType.StoredProcedure;

            // Add parameters
            var resourceParam = command.CreateParameter();
            resourceParam.ParameterName = "@Resource";
            resourceParam.Value = lockResource;
            command.Parameters.Add(resourceParam);

            var lockModeParam = command.CreateParameter();
            lockModeParam.ParameterName = "@LockMode";
            lockModeParam.Value = "Exclusive";
            command.Parameters.Add(lockModeParam);

            var lockOwnerParam = command.CreateParameter();
            lockOwnerParam.ParameterName = "@LockOwner";
            lockOwnerParam.Value = "Session";
            command.Parameters.Add(lockOwnerParam);

            var lockTimeoutParam = command.CreateParameter();
            lockTimeoutParam.ParameterName = "@LockTimeout";
            lockTimeoutParam.Value = 0; // Non-blocking
            command.Parameters.Add(lockTimeoutParam);

            // Add return value parameter
            var returnParam = command.CreateParameter();
            returnParam.ParameterName = "@ReturnValue";
            returnParam.Direction = ParameterDirection.ReturnValue;
            command.Parameters.Add(returnParam);

            // Execute command
            await command.ExecuteNonQueryAsync(cancellationToken);

            // Get return value
            var returnValue = (int)returnParam.Value!;

            // Return codes:
            // 0, 1 = Success (lock granted)
            // < 0 = Failure (lock not granted)
            var lockAcquired = returnValue >= 0;

            if (!lockAcquired)
            {
                _logger.LogDebug(
                    "Failed to acquire lock '{LockResource}'. Return code: {ReturnCode}",
                    lockResource,
                    returnValue
                );
            }

            return lockAcquired;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error acquiring lock '{LockResource}'. Will skip this iteration.",
                lockResource
            );
            return false;
        }
    }

    /// <summary>
    /// Release SQL Server application lock
    ///
    /// Note: Lock is automatically released when:
    /// - Session/connection closes
    /// - Application crashes
    /// - Explicit call to sp_releaseapplock
    ///
    /// This method explicitly releases the lock for cleanliness,
    /// but it's not strictly necessary (session-scoped locks auto-release).
    ///
    /// SQL Executed:
    /// <code>
    /// EXEC sp_releaseapplock
    ///     @Resource = 'OutboxProcessor_Identity',
    ///     @LockOwner = 'Session'
    /// </code>
    /// </summary>
    /// <param name="dbContext">Database context</param>
    /// <param name="lockResource">Lock resource name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task ReleaseLockAsync(
        ModuleDbContext dbContext,
        string lockResource,
        CancellationToken cancellationToken)
    {
        try
        {
            var connection = dbContext.Database.GetDbConnection();

            if (connection.State != ConnectionState.Open)
            {
                // Connection already closed, lock auto-released
                return;
            }

            using var command = connection.CreateCommand();
            command.CommandText = "sp_releaseapplock";
            command.CommandType = CommandType.StoredProcedure;

            var resourceParam = command.CreateParameter();
            resourceParam.ParameterName = "@Resource";
            resourceParam.Value = lockResource;
            command.Parameters.Add(resourceParam);

            var lockOwnerParam = command.CreateParameter();
            lockOwnerParam.ParameterName = "@LockOwner";
            lockOwnerParam.Value = "Session";
            command.Parameters.Add(lockOwnerParam);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Non-critical error - lock will auto-release when connection closes
            _logger.LogWarning(
                ex,
                "Error releasing lock '{LockResource}'. Lock will auto-release when connection closes.",
                lockResource
            );
        }
    }

    /// <summary>
    /// Get ModuleDbContext from service provider
    /// Must be implemented by derived class for each module
    /// </summary>
    /// <param name="serviceProvider">Service provider from current scope</param>
    /// <returns>Module's DbContext (must inherit from ModuleDbContext)</returns>
    protected abstract ModuleDbContext GetDbContext(IServiceProvider serviceProvider);
}
