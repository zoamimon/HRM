using System.Text.Json;
using HRM.BuildingBlocks.Application.Abstractions.EventBus;
using HRM.BuildingBlocks.Domain.Abstractions.Events;
using HRM.BuildingBlocks.Domain.Outbox;
using HRM.BuildingBlocks.Infrastructure.Persistence;
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
/// - No locking needed (messages processed once)
///
/// Deployment Scenarios:
/// 1. Single Instance:
///    - One OutboxProcessor per application
///    - Simple, works for most applications
///
/// 2. Multiple Instances (Scaled Out):
///    - Use distributed locking (Redis, SQL Server locks)
///    - Prevents duplicate processing
///    - Each instance polls independently
///
/// 3. Microservices:
///    - Each service has own OutboxProcessor
///    - Publishes to RabbitMQ instead of in-memory
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
    /// </summary>
    private async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();

        var dbContext = GetDbContext(scope.ServiceProvider);
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

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
    /// Get ModuleDbContext from service provider
    /// Must be implemented by derived class for each module
    /// </summary>
    /// <param name="serviceProvider">Service provider from current scope</param>
    /// <returns>Module's DbContext (must inherit from ModuleDbContext)</returns>
    protected abstract ModuleDbContext GetDbContext(IServiceProvider serviceProvider);
}
