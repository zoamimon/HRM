using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HRM.BuildingBlocks.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior for logging request/response information.
/// Logs all commands and queries with execution time and result status.
/// 
/// Pipeline Position:
/// This behavior wraps the entire pipeline (first in, last out):
/// <code>
/// 1. LoggingBehavior (START) ← logs request
/// 2. ValidationBehavior
/// 3. Handler (Command/Query)
/// 4. UnitOfWorkBehavior (commands only)
/// 5. LoggingBehavior (END) ← logs response/result
/// </code>
/// 
/// What Gets Logged:
/// 
/// Request (Information Level):
/// - Request type name
/// - Request parameters (sanitized)
/// - Timestamp
/// 
/// Response Success (Information Level):
/// - Request type name
/// - Execution time (milliseconds)
/// - Result status (Success/Failure)
/// - Response value (if any, sanitized)
/// 
/// Response Failure (Warning Level):
/// - Request type name
/// - Execution time
/// - Error details
/// - Error code and message
/// 
/// Exception (Error Level):
/// - Request type name
/// - Exception details
/// - Stack trace
/// 
/// Logging Examples:
/// <code>
/// // Command Success:
/// [Information] Executing command: RegisterOperatorCommand
/// [Information] RegisterOperatorCommand completed successfully in 245ms
/// 
/// // Command Failure:
/// [Warning] RegisterOperatorCommand failed in 156ms: Operator.UsernameExists - Username 'admin' already exists
/// 
/// // Query Success:
/// [Information] Executing query: GetOperatorByIdQuery
/// [Information] GetOperatorByIdQuery completed successfully in 23ms
/// 
/// // Exception:
/// [Error] RegisterOperatorCommand threw exception: System.Data.SqlClient.SqlException: Violation of UNIQUE KEY constraint...
/// </code>
/// 
/// Sensitive Data Handling:
/// CRITICAL: Never log sensitive information!
/// 
/// <code>
/// // ❌ BAD - Don't log these:
/// - Passwords (plaintext or hashed)
/// - Tokens (JWT, refresh tokens)
/// - Social security numbers
/// - Credit card numbers
/// - Personal health information
/// - API keys, secrets
/// 
/// // ✅ GOOD - Safe to log:
/// - Usernames (non-sensitive)
/// - Email addresses (with consent)
/// - Entity IDs (GUIDs)
/// - Business entity names
/// - Timestamps
/// - Error codes and messages
/// </code>
/// 
/// Sanitization Strategy:
/// Override ToString() in commands to exclude sensitive data:
/// 
/// <code>
/// public sealed record RegisterOperatorCommand : ICommand&lt;Guid&gt;
/// {
///     public string Username { get; init; }
///     public string Email { get; init; }
///     public string Password { get; init; } // Sensitive!
///     
///     // Override ToString to exclude password
///     public override string ToString()
///     {
///         return $"RegisterOperatorCommand {{ Username = {Username}, Email = {Email}, Password = [REDACTED] }}";
///     }
/// }
/// </code>
/// 
/// Alternative: Use custom logger that filters properties
/// <code>
/// private string SanitizeRequest&lt;TRequest&gt;(TRequest request)
/// {
///     var properties = typeof(TRequest).GetProperties()
///         .Where(p => !IsSensitiveProperty(p.Name))
///         .Select(p => $"{p.Name}={p.GetValue(request)}");
///     
///     return string.Join(", ", properties);
/// }
/// 
/// private bool IsSensitiveProperty(string propertyName)
/// {
///     var sensitiveNames = new[] { "Password", "Token", "Secret", "ApiKey", "SSN", "CreditCard" };
///     return sensitiveNames.Any(s => propertyName.Contains(s, StringComparison.OrdinalIgnoreCase));
/// }
/// </code>
/// 
/// Performance Monitoring:
/// This behavior measures execution time for performance tracking:
/// 
/// <code>
/// // Slow query detection:
/// if (elapsedMilliseconds > 1000)
/// {
///     _logger.LogWarning(
///         "Slow query detected: {RequestName} took {ElapsedMilliseconds}ms",
///         requestName,
///         elapsedMilliseconds
///     );
/// }
/// </code>
/// 
/// Structured Logging:
/// Use structured logging for better queryability:
/// 
/// <code>
/// _logger.LogInformation(
///     "Executing {RequestType} with parameters {@RequestParameters}",
///     typeof(TRequest).Name,
///     request // Will be serialized to JSON by logger
/// );
/// </code>
/// 
/// Log Levels:
/// - Information: Normal execution, successful operations
/// - Warning: Business failures (validation errors, not found, etc.)
/// - Error: Exceptions, unexpected errors
/// - Debug: Detailed diagnostic information (development only)
/// 
/// Production Configuration:
/// <code>
/// // appsettings.Production.json
/// {
///   "Logging": {
///     "LogLevel": {
///       "Default": "Information",
///       "Microsoft": "Warning",
///       "Microsoft.EntityFrameworkCore": "Warning",
///       "HRM.BuildingBlocks.Application.Behaviors": "Information"
///     }
///   }
/// }
/// </code>
/// 
/// Integration with Application Insights:
/// <code>
/// // Program.cs
/// builder.Services.AddApplicationInsightsTelemetry();
/// 
/// // Logs automatically sent to Application Insights
/// // Can query with Kusto:
/// // traces
/// // | where message contains "RegisterOperatorCommand"
/// // | where customDimensions.ElapsedMilliseconds > 1000
/// </code>
/// 
/// Testing:
/// <code>
/// public class LoggingBehaviorTests
/// {
///     [Fact]
///     public async Task Handle_ShouldLogRequestAndResponse()
///     {
///         // Arrange
///         var logger = new FakeLogger&lt;LoggingBehavior&lt;TestCommand, Guid&gt;&gt;();
///         var behavior = new LoggingBehavior&lt;TestCommand, Guid&gt;(logger);
///         
///         // Act
///         await behavior.Handle(
///             new TestCommand(),
///             () => Task.FromResult(Result.Success(Guid.NewGuid())),
///             CancellationToken.None
///         );
///         
///         // Assert
///         logger.Logs.Should().Contain(l => l.Contains("Executing"));
///         logger.Logs.Should().Contain(l => l.Contains("completed successfully"));
///     }
/// }
/// </code>
/// </summary>
/// <typeparam name="TRequest">Type of request (command or query)</typeparam>
/// <typeparam name="TResponse">Type of response</typeparam>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Handles logging for request/response pipeline.
    /// Logs request start, measures execution time, and logs result.
    /// </summary>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        // Log request start
        _logger.LogInformation(
            "Executing {RequestName}",
            requestName
        );

        // Start timing
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Execute next behavior/handler in pipeline
            var response = await next();

            stopwatch.Stop();
            var elapsedMilliseconds = stopwatch.ElapsedMilliseconds;

            // Log based on response type
            LogResponse(requestName, response, elapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "{RequestName} threw exception after {ElapsedMilliseconds}ms",
                requestName,
                stopwatch.ElapsedMilliseconds
            );

            throw;
        }
    }

    /// <summary>
    /// Logs response based on type (Result vs direct value).
    /// </summary>
    private void LogResponse(string requestName, TResponse response, long elapsedMilliseconds)
    {
        // Check if response is Result type (commands)
        if (response is Results.Result result)
        {
            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "{RequestName} completed successfully in {ElapsedMilliseconds}ms",
                    requestName,
                    elapsedMilliseconds
                );
            }
            else
            {
                _logger.LogWarning(
                    "{RequestName} failed in {ElapsedMilliseconds}ms: {ErrorCode} - {ErrorMessage}",
                    requestName,
                    elapsedMilliseconds,
                    result.Error.Code,
                    result.Error.Message
                );
            }
        }
        else
        {
            // Query response (direct value, no Result wrapper)
            _logger.LogInformation(
                "{RequestName} completed successfully in {ElapsedMilliseconds}ms",
                requestName,
                elapsedMilliseconds
            );
        }

        // Warn if slow operation (> 1 second)
        if (elapsedMilliseconds > 1000)
        {
            _logger.LogWarning(
                "Slow operation detected: {RequestName} took {ElapsedMilliseconds}ms",
                requestName,
                elapsedMilliseconds
            );
        }
    }
}
