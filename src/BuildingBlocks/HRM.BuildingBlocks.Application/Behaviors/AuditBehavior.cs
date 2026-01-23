using HRM.BuildingBlocks.Application.Abstractions.Commands;
using HRM.BuildingBlocks.Application.Abstractions.Infrastructure;
using MediatR;

namespace HRM.BuildingBlocks.Application.Behaviors;

/// <summary>
/// Pipeline behavior for automatic audit logging injection.
///
/// Responsibilities:
/// - Detect commands implementing IAuditableCommand
/// - Extract IP address and User Agent from IClientInfoService
/// - Inject audit values into command properties before handler execution
/// - Enable consistent audit logging without manual parameter passing
///
/// Position in Pipeline: FIRST (before Validation and UnitOfWork)
/// - Injects audit data before validation
/// - Ensures handler receives complete command with audit info
/// - Runs before any business logic
///
/// How It Works:
/// 1. Check if request implements IAuditableCommand
/// 2. If yes:
///    - Get IpAddress from IClientInfoService
///    - Get UserAgent from IClientInfoService
///    - Set properties on command using reflection
/// 3. Continue pipeline with enriched command
///
/// Benefits:
/// - Consistent audit logging across all commands
/// - No manual IClientInfoService injection in endpoints
/// - Centralized audit logic
/// - Commands remain clean and focused on business intent
///
/// Example:
/// <code>
/// // Command definition (no IP/UserAgent in constructor)
/// public sealed record LogoutCommand(string RefreshToken) : IModuleCommand, IAuditableCommand
/// {
///     public string? IpAddress { get; set; }
///     public string? UserAgent { get; set; }
/// }
///
/// // API endpoint (no IClientInfoService needed)
/// group.MapPost("/logout", async (LogoutRequest request, ISender sender) =>
/// {
///     var command = new LogoutCommand(request.RefreshToken);
///     // AuditBehavior automatically injects IP/UserAgent here
///     var result = await sender.Send(command);
///     return Results.Ok();
/// });
///
/// // Handler receives command with audit data populated
/// public async Task&lt;Result&gt; Handle(LogoutCommand command, CancellationToken ct)
/// {
///     // command.IpAddress and command.UserAgent are already set
///     // ...
/// }
/// </code>
///
/// Thread Safety:
/// - IClientInfoService is scoped per HTTP request
/// - Behavior is transient (new instance per request)
/// - Safe for concurrent requests
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public sealed class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IClientInfoService _clientInfoService;

    public AuditBehavior(IClientInfoService clientInfoService)
    {
        _clientInfoService = clientInfoService;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Check if request implements IAuditableCommand
        if (request is IAuditableCommand auditableCommand)
        {
            // Inject audit data from IClientInfoService
            auditableCommand.IpAddress = _clientInfoService.IpAddress;
            auditableCommand.UserAgent = _clientInfoService.UserAgent;
        }

        // Continue pipeline
        return await next();
    }
}
