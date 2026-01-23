using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HRM.BuildingBlocks.Application.Behaviors;

/// <summary>
/// Pipeline behavior for logging requests and responses
/// 
/// Responsibilities:
/// - Log incoming requests
/// - Measure execution time
/// - Log responses or errors
/// - Provide audit trail
/// 
/// Position in Pipeline: FIRST (outermost)
/// - Wraps all other behaviors
/// - Logs even if validation/execution fails
/// - Ensures complete observability
/// 
/// Example Log Output:
/// [INFO] Handling RegisterOperatorCommand
/// [INFO] Handled RegisterOperatorCommand in 245ms
/// [ERROR] Error handling RegisterOperatorCommand: Username already exists
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        _logger.LogInformation(
            "Handling {RequestName}",
            requestName);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next();

            stopwatch.Stop();

            _logger.LogInformation(
                "Handled {RequestName} in {ElapsedMilliseconds}ms",
                requestName,
                stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "Error handling {RequestName} after {ElapsedMilliseconds}ms",
                requestName,
                stopwatch.ElapsedMilliseconds);

            throw;  // Re-throw to preserve stack trace
        }
    }
}
