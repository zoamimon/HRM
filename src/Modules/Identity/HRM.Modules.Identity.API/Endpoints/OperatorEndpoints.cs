using HRM.BuildingBlocks.Infrastructure.Extensions;
using HRM.Modules.Identity.API.Contracts;
using HRM.Modules.Identity.Application.Commands.ActivateOperator;
using HRM.Modules.Identity.Application.Commands.RegisterOperator;
using HRM.Modules.Identity.Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HRM.Modules.Identity.API.Endpoints;

/// <summary>
/// Minimal API endpoints for Operator operations.
/// Implements RESTful API for operator registration and activation.
///
/// Architecture:
/// - Minimal API: Lightweight alternative to Controllers
/// - CQRS: Commands via MediatR (RegisterOperator, ActivateOperator)
/// - Result Pattern: Type-safe error handling with DomainError
/// - Pure DDD: Domain errors mapped to HTTP in API layer
/// - DTO Mapping: Request DTOs → Commands, Entities → Response DTOs
///
/// Endpoints:
/// 1. POST /api/identity/operators/register
///    - Register new operator (admin-only)
///    - Returns: 201 Created with OperatorResponse
///
/// 2. POST /api/identity/operators/{id}/activate
///    - Activate pending operator (admin-only)
///    - Returns: 200 OK with OperatorResponse
///
/// Authorization:
/// - All endpoints require authentication (Bearer JWT)
/// - RequireAuthorization() adds [Authorize] behavior
/// - Policy-based: "AdminOnly" policy (role = Admin)
///
/// Error Handling:
/// - ResultExtensions.ToHttpResultAsync() maps DomainError → HTTP
/// - NotFoundError → 404 Not Found
/// - ConflictError → 409 Conflict
/// - ValidationError → 400 Bad Request
/// - UnauthorizedError → 401 Unauthorized
/// - ForbiddenError → 403 Forbidden
///
/// Example Usage (Startup/Program.cs):
/// <code>
/// app.MapIdentityEndpoints();
/// </code>
///
/// Request/Response Examples:
/// See RegisterOperatorRequest.cs and OperatorResponse.cs
/// </summary>
public static class OperatorEndpoints
{
    /// <summary>
    /// Map operator endpoints to route builder.
    /// </summary>
    /// <param name="app">Endpoint route builder</param>
    /// <returns>Route group builder for chaining</returns>
    public static IEndpointRouteBuilder MapOperatorEndpoints(this IEndpointRouteBuilder app)
    {
        // Create route group: /api/identity/operators
        var group = app.MapGroup("/api/identity/operators")
            .WithTags("Operators")
            .RequireAuthorization(); // All endpoints require authentication

        // 1. Register operator
        group.MapPost("/register", RegisterOperator)
            .WithName("RegisterOperator")
            .WithSummary("Register a new operator")
            .WithDescription("Create a new operator account in Pending status. Admin-only operation.")
            .Produces<OperatorResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // 2. Activate operator
        group.MapPost("/{id:guid}/activate", ActivateOperator)
            .WithName("ActivateOperator")
            .WithSummary("Activate a pending operator")
            .WithDescription("Change operator status from Pending to Active. Admin-only operation.")
            .Produces<OperatorResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    /// <summary>
    /// POST /api/identity/operators/register
    /// Register a new operator (admin-only).
    /// </summary>
    private static async Task<IResult> RegisterOperator(
        RegisterOperatorRequest request,
        ISender sender,
        IOperatorRepository operatorRepository,
        CancellationToken cancellationToken)
    {
        // Map request DTO to command
        var command = new RegisterOperatorCommand(
            Username: request.Username,
            Email: request.Email,
            Password: request.Password,
            FullName: request.FullName,
            PhoneNumber: request.PhoneNumber
        );

        // Execute command via MediatR
        var result = await sender.Send(command, cancellationToken);

        // Map Result<Guid> to HTTP response using ResultExtensions
        // DomainError → HTTP status codes happen here
        return await result.ToHttpResultAsync(async operatorId =>
        {
            // Retrieve created operator
            var @operator = await operatorRepository.GetByIdAsync(operatorId, cancellationToken);

            if (@operator is null)
            {
                return Results.Problem(
                    detail: "Operator was created but could not be retrieved.",
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }

            // Map entity to response DTO
            var response = new OperatorResponse(
                Id: @operator.Id,
                Username: @operator.Username,
                Email: @operator.Email,
                FullName: @operator.FullName,
                PhoneNumber: @operator.PhoneNumber,
                Status: @operator.Status.ToString(),
                IsTwoFactorEnabled: @operator.IsTwoFactorEnabled,
                ActivatedAtUtc: @operator.ActivatedAtUtc,
                LastLoginAtUtc: @operator.LastLoginAtUtc,
                CreatedAtUtc: @operator.CreatedAtUtc,
                ModifiedAtUtc: @operator.ModifiedAtUtc
            );

            // Return 201 Created with Location header
            return Results.Created($"/api/identity/operators/{operatorId}", response);
        });
    }

    /// <summary>
    /// POST /api/identity/operators/{id}/activate
    /// Activate a pending operator (admin-only).
    /// </summary>
    private static async Task<IResult> ActivateOperator(
        Guid id,
        ISender sender,
        IOperatorRepository operatorRepository,
        CancellationToken cancellationToken)
    {
        // Create command
        var command = new ActivateOperatorCommand(OperatorId: id);

        // Execute command via MediatR
        var result = await sender.Send(command, cancellationToken);

        // Map Result to HTTP response using ResultExtensions
        // For void Result, we need custom success handling
        if (result.IsSuccess)
        {
            // Retrieve activated operator
            var @operator = await operatorRepository.GetByIdAsync(id, cancellationToken);

            if (@operator is null)
            {
                return Results.Problem(
                    detail: "Operator was activated but could not be retrieved.",
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }

            // Map entity to response DTO
            var response = new OperatorResponse(
                Id: @operator.Id,
                Username: @operator.Username,
                Email: @operator.Email,
                FullName: @operator.FullName,
                PhoneNumber: @operator.PhoneNumber,
                Status: @operator.Status.ToString(),
                IsTwoFactorEnabled: @operator.IsTwoFactorEnabled,
                ActivatedAtUtc: @operator.ActivatedAtUtc,
                LastLoginAtUtc: @operator.LastLoginAtUtc,
                CreatedAtUtc: @operator.CreatedAtUtc,
                ModifiedAtUtc: @operator.ModifiedAtUtc
            );

            // Return 200 OK with response body
            return Results.Ok(response);
        }

        // Map error using ResultExtensions
        // This handles all DomainError types (NotFoundError, ValidationError, etc.)
        return result.ToHttpResult();
    }
}
