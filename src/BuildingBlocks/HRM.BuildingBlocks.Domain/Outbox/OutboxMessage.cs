using HRM.BuildingBlocks.Domain.Entities;

namespace HRM.BuildingBlocks.Domain.Outbox;

/// <summary>
/// Outbox message entity for reliable event publishing
/// Implements the Transactional Outbox pattern
/// 
/// Pattern Purpose:
/// - Guarantee event publishing even if application crashes
/// - Atomic transaction: domain changes + outbox message saved together
/// - Background service processes messages asynchronously
/// - Ensures eventual consistency across modules
/// - Prevents message loss (database is source of truth)
/// 
/// How It Works:
/// 1. Domain operation executes (e.g., Register Operator)
/// 2. Domain event handler creates integration event
/// 3. Integration event serialized to JSON
/// 4. OutboxMessage created with serialized event
/// 5. Both domain entity and OutboxMessage saved in SAME transaction
/// 6. If commit succeeds, message guaranteed to be processed later
/// 7. Background service queries unprocessed messages
/// 8. Publishes events via event bus
/// 9. Marks messages as processed
/// 10. Cleanup processed messages (optional)
/// 
/// Per-Module Tables (Microservices Ready):
/// - [identity].[OutboxMessages] (Identity module events)
/// - [personnel].[OutboxMessages] (Personnel module events)
/// - [organization].[OutboxMessages] (Organization module events)
/// 
/// Benefits:
/// - Easy to split into microservices (each module owns its outbox)
/// - No shared database table (module independence)
/// - Clean separation of concerns
/// 
/// Retry Logic:
/// - Failed messages can be retried up to MaxAttempts
/// - AttemptCount incremented on each failure
/// - After max retries, message moved to dead letter queue
/// - Manual intervention required for dead letter messages
/// </summary>
public sealed class OutboxMessage : AuditableEntity, IAggregateRoot
{
    /// <summary>
    /// Full type name of the integration event
    /// Used for deserialization when processing the message
    /// 
    /// Format: Namespace.ClassName, AssemblyName
    /// Example: "HRM.Modules.Identity.IntegrationEvents.OperatorRegisteredIntegrationEvent, HRM.Modules.Identity.IntegrationEvents"
    /// 
    /// Why full type name:
    /// - Enables dynamic deserialization via Type.GetType() or registry
    /// - Supports event versioning (different versions in different assemblies)
    /// - Allows type resolution across assembly boundaries
    /// 
    /// ✅ Public property with private set (EF Core compatible)
    /// </summary>
    public string Type { get; private set; } = string.Empty;

    /// <summary>
    /// Serialized JSON content of the integration event
    /// Contains all event data needed by subscribers
    /// 
    /// Serialization:
    /// - Uses System.Text.Json (built-in .NET)
    /// - Compact format (no formatting)
    /// - UTF-8 encoding
    /// 
    /// Example content:
    /// {
    ///   "Id": "guid",
    ///   "OccurredOnUtc": "2026-01-10T10:30:00Z",
    ///   "OperatorId": "guid",
    ///   "Username": "admin",
    ///   "Email": "admin@example.com"
    /// }
    /// 
    /// ✅ Public property with private set (EF Core compatible)
    /// </summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>
    /// When the event originally occurred in the domain
    /// NOT when the OutboxMessage was created, but when the domain event happened
    /// 
    /// ✅ CRITICAL: This is passed from IntegrationEvent.OccurredOnUtc
    ///    NOT DateTime.UtcNow in OutboxMessage.Create()
    /// 
    /// Event Timeline Example:
    /// - 10:30:00.000 → Operator.Register() executes (domain operation)
    /// - 10:30:00.001 → OperatorRegisteredDomainEvent raised (OccurredOnUtc = 10:30:00.000)
    /// - 10:30:00.050 → Domain event handler creates OperatorRegisteredIntegrationEvent
    ///                  (OccurredOnUtc = domainEvent.OccurredOnUtc = 10:30:00.000)
    /// - 10:30:00.100 → OutboxMessage.Create called with integrationEvent.OccurredOnUtc
    ///                  (OccurredOnUtc = 10:30:00.000) ✅ CORRECT
    /// - 10:30:00.200 → Transaction commits
    /// - 10:30:15.000 → Background service processes message
    /// 
    /// Why Important:
    /// - Event Ordering: Background service processes in OccurredOnUtc order
    /// - Causality: Track which domain event caused which integration event
    /// - Audit Trail: Know exact time of business operation
    /// - Debugging: Reconstruct event timeline accurately
    /// 
    /// Used for:
    /// - Ordering: Background service processes in chronological order
    /// - Causality: Ensures effects happen after causes
    /// - Debugging: Trace event timeline
    /// - Auditing: Track business operation timing
    /// 
    /// ✅ Public property with private set (EF Core compatible)
    /// </summary>
    public DateTime OccurredOnUtc { get; private set; }

    /// <summary>
    /// When the message was successfully processed and published
    /// 
    /// Values:
    /// - NULL: Not yet processed (pending)
    /// - NOT NULL: Already processed (can be archived/deleted)
    /// 
    /// Query usage:
    /// - WHERE ProcessedOnUtc IS NULL (get unprocessed)
    /// - WHERE ProcessedOnUtc IS NOT NULL (get processed)
    /// - WHERE ProcessedOnUtc > @SomeDate (cleanup old processed)
    /// 
    /// ✅ Public property with private set (EF Core compatible)
    /// </summary>
    public DateTime? ProcessedOnUtc { get; private set; }

    /// <summary>
    /// Error message if processing failed
    /// 
    /// Values:
    /// - NULL: No error (successful or not yet processed)
    /// - NOT NULL: Processing failed, contains error details
    /// 
    /// Used for:
    /// - Debugging: What went wrong?
    /// - Retry logic: Identify transient vs permanent failures
    /// - Monitoring: Alert on error patterns
    /// - Dead letter: Move to dead letter after max retries
    /// 
    /// Example errors:
    /// - "Unknown event type: SomeEvent"
    /// - "Failed to deserialize event: Invalid JSON"
    /// - "Event handler threw exception: NullReferenceException"
    /// - "Event bus publish failed: Connection timeout"
    /// 
    /// Max length: 2000 characters (configured in EF configuration)
    /// 
    /// ✅ Public property with private set (EF Core compatible)
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// Number of processing attempts
    /// Incremented each time processing fails
    /// 
    /// Used for:
    /// - Retry limit: Stop after MaxAttempts (default: 3)
    /// - Dead letter: Move to dead letter queue when limit exceeded
    /// - Monitoring: Track problematic messages
    /// - Alerting: Alert when messages repeatedly fail
    /// 
    /// Workflow:
    /// 1. Initial state: AttemptCount = 0
    /// 2. First processing attempt fails: AttemptCount = 1
    /// 3. Second attempt fails: AttemptCount = 2
    /// 4. Third attempt fails: AttemptCount = 3 (max reached)
    /// 5. Message moved to dead letter queue
    /// 6. Manual intervention required
    /// 
    /// Query usage:
    /// - WHERE AttemptCount < @MaxAttempts (get retry candidates)
    /// - WHERE AttemptCount >= @MaxAttempts (get dead letter)
    /// 
    /// ✅ Public property with private set (EF Core compatible)
    /// </summary>
    public int AttemptCount { get; private set; }

    /// <summary>
    /// Private parameterless constructor for EF Core
    /// EF Core needs this to materialize entities from database
    /// Not used in application code
    /// </summary>
    private OutboxMessage()
    {
    }

    /// <summary>
    /// Factory method to create a new outbox message
    /// Enforces invariants and ensures valid creation
    /// 
    /// ✅ FIXED: Now accepts occurredOnUtc from IntegrationEvent
    ///    instead of using DateTime.UtcNow
    /// 
    /// Private constructor + factory pattern ensures:
    /// - Cannot create invalid OutboxMessage
    /// - All required data must be provided
    /// - Consistent initialization
    /// - Proper event timing preserved
    /// 
    /// Usage Example:
    /// <code>
    /// // In domain event handler
    /// var integrationEvent = new OperatorRegisteredIntegrationEvent
    /// {
    ///     Id = Guid.NewGuid(),
    ///     OccurredOnUtc = domainEvent.OccurredOnUtc,  // ← From domain event
    ///     OperatorId = domainEvent.OperatorId,
    ///     Username = domainEvent.Username,
    ///     Email = domainEvent.Email
    /// };
    /// 
    /// var json = JsonSerializer.Serialize(integrationEvent);
    /// 
    /// var outboxMessage = OutboxMessage.Create(
    ///     type: integrationEvent.GetType().AssemblyQualifiedName!,
    ///     content: json,
    ///     occurredOnUtc: integrationEvent.OccurredOnUtc  // ← Pass event time
    /// );
    /// 
    /// await dbContext.OutboxMessages.AddAsync(outboxMessage);
    /// </code>
    /// </summary>
    /// <param name="type">Full type name of the integration event</param>
    /// <param name="content">Serialized JSON content</param>
    /// <param name="occurredOnUtc">When the domain event occurred (from IntegrationEvent)</param>
    /// <returns>New outbox message ready to be saved</returns>
    /// <exception cref="ArgumentException">If type or content is null/empty</exception>
    public static OutboxMessage Create(
        string type,
        string content,
        DateTime occurredOnUtc)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Type cannot be null or empty", nameof(type));

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be null or empty", nameof(content));

        // Create new message with event's original timestamp
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = type,
            Content = content,
            OccurredOnUtc = occurredOnUtc,  // ✅ From integration event, NOT DateTime.UtcNow
            ProcessedOnUtc = null,
            Error = null,
            AttemptCount = 0
        };
    }

    /// <summary>
    /// Mark message as successfully processed
    /// Called by background service after publishing event via event bus
    /// 
    /// Side effects:
    /// - Sets ProcessedOnUtc to current time
    /// - Clears any previous error
    /// - Message will no longer appear in unprocessed queries
    /// </summary>
    public void MarkAsProcessed()
    {
        ProcessedOnUtc = DateTime.UtcNow;
        Error = null; // Clear any previous error from retry attempts
    }

    /// <summary>
    /// Mark message as failed with error details
    /// Called by background service if publishing fails
    /// 
    /// Side effects:
    /// - Sets error message
    /// - Increments attempt count
    /// - ProcessedOnUtc remains NULL (message will be retried)
    /// - If AttemptCount reaches max, becomes dead letter
    /// </summary>
    /// <param name="error">Error message describing what went wrong</param>
    public void MarkAsFailed(string error)
    {
        Error = error;
        AttemptCount++;
        // Note: ProcessedOnUtc remains NULL so message will be retried
    }

    /// <summary>
    /// Check if message can be retried
    /// Messages can be retried if:
    /// - Not yet processed (ProcessedOnUtc is NULL)
    /// - Attempt count below max retry limit
    /// </summary>
    /// <param name="maxAttempts">Maximum retry attempts allowed (default: 3)</param>
    /// <returns>True if can retry, false if exceeded max attempts (dead letter)</returns>
    public bool CanRetry(int maxAttempts = 3)
    {
        return AttemptCount < maxAttempts && ProcessedOnUtc == null;
    }

    /// <summary>
    /// Check if message has been processed
    /// </summary>
    public bool IsProcessed() => ProcessedOnUtc.HasValue;

    /// <summary>
    /// Check if message has error
    /// </summary>
    public bool HasError() => !string.IsNullOrEmpty(Error);
}
