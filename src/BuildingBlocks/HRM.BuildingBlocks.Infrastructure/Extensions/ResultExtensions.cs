using HRM.BuildingBlocks.Domain.Abstractions.Results;
using Microsoft.AspNetCore.Http;

namespace HRM.BuildingBlocks.Infrastructure.Extensions;

/// <summary>
/// Extension methods for mapping Result pattern to HTTP responses.
/// Converts pure DomainError to HTTP status codes for ASP.NET Core Minimal API.
///
/// Architecture:
/// - Domain Layer: Pure DomainError (no HTTP knowledge)
/// - Application Layer: Returns Result<T> with DomainError
/// - Infrastructure/API Layer: Maps DomainError → HTTP (THIS FILE)
///
/// Design Principle:
/// Domain remains pure and transport-agnostic.
/// HTTP mapping happens at the edges (API layer).
///
/// Usage in Minimal API Endpoints:
/// <code>
/// app.MapPost("/api/operators/register", async (
///     RegisterOperatorRequest request,
///     ISender sender) =>
/// {
///     var command = new RegisterOperatorCommand(...);
///     var result = await sender.Send(command);
///
///     // Clean HTTP mapping
///     return result.ToHttpResult(operatorId =>
///         Results.Created($"/api/operators/{operatorId}", new { Id = operatorId })
///     );
/// });
/// </code>
///
/// HTTP Status Code Mapping:
/// - NotFoundError → 404 Not Found
/// - ConflictError → 409 Conflict
/// - ValidationError → 400 Bad Request (with field-level details)
/// - UnauthorizedError → 401 Unauthorized
/// - ForbiddenError → 403 Forbidden
/// - FailureError → 500 Internal Server Error
/// - Unknown DomainError → 500 Internal Server Error (fallback)
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Convert Result (void) to IResult.
    /// Maps success to 204 No Content.
    /// Maps failure to appropriate HTTP status.
    ///
    /// Usage:
    /// <code>
    /// var result = await sender.Send(new DeleteOperatorCommand(id));
    /// return result.ToHttpResult(); // 204 No Content or error status
    /// </code>
    /// </summary>
    public static IResult ToHttpResult(this Result result)
    {
        if (result.IsSuccess)
            return Results.NoContent();

        return MapErrorToHttp(result.Error!);
    }

    /// <summary>
    /// Convert Result<T> to IResult with custom success handler.
    /// Allows full control over success response (status code, headers, body).
    ///
    /// Usage:
    /// <code>
    /// var result = await sender.Send(new RegisterOperatorCommand(...));
    /// return result.ToHttpResult(operatorId =>
    ///     Results.Created($"/api/operators/{operatorId}", new { Id = operatorId })
    /// );
    /// </code>
    /// </summary>
    /// <typeparam name="T">Type of value in Result</typeparam>
    /// <param name="result">Result to convert</param>
    /// <param name="onSuccess">Function to create success response from value</param>
    public static IResult ToHttpResult<T>(
        this Result<T> result,
        Func<T, IResult> onSuccess)
    {
        if (result.IsSuccess)
            return onSuccess(result.Value);

        return MapErrorToHttp(result.Error!);
    }

    /// <summary>
    /// Convert Result<T> to IResult with default 200 OK response.
    /// Returns value as JSON body with 200 OK status.
    ///
    /// Usage:
    /// <code>
    /// var result = await sender.Send(new GetOperatorQuery(id));
    /// return result.ToHttpResult(); // 200 OK with value as JSON
    /// </code>
    /// </summary>
    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        return result.ToHttpResult(value => Results.Ok(value));
    }

    /// <summary>
    /// Maps DomainError to HTTP IResult.
    /// Central mapping logic for all error types.
    ///
    /// Pattern Matching:
    /// Uses C# pattern matching for type-safe error mapping.
    /// Ensures all DomainError types are handled.
    /// </summary>
    private static IResult MapErrorToHttp(DomainError error)
    {
        return error switch
        {
            // 404 Not Found - Resource doesn't exist
            NotFoundError notFound => Results.NotFound(new
            {
                notFound.Code,
                notFound.Message
            }),

            // 409 Conflict - Duplicate key, state conflict
            ConflictError conflict => Results.Conflict(new
            {
                conflict.Code,
                conflict.Message
            }),

            // 400 Bad Request - Validation failure
            ValidationError validation => Results.BadRequest(new
            {
                validation.Code,
                validation.Message,
                validation.Details
            }),

            // 401 Unauthorized - Authentication failure
            UnauthorizedError unauthorized => Results.Json(
                new
                {
                    unauthorized.Code,
                    unauthorized.Message
                },
                statusCode: StatusCodes.Status401Unauthorized),

            // 403 Forbidden - Authorization failure
            ForbiddenError forbidden => Results.Json(
                new
                {
                    forbidden.Code,
                    forbidden.Message
                },
                statusCode: StatusCodes.Status403Forbidden),

            // 500 Internal Server Error - System failure
            FailureError failure => Results.Problem(
                detail: failure.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "System Error"),

            // Fallback for unknown error types (should not happen)
            _ => Results.Problem(
                detail: error.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Unexpected Error")
        };
    }

    /// <summary>
    /// Async variant of ToHttpResult for Result<T>.
    /// Useful when success handler is async.
    ///
    /// Usage:
    /// <code>
    /// return await result.ToHttpResultAsync(async operatorId =>
    /// {
    ///     var @operator = await repository.GetByIdAsync(operatorId);
    ///     return Results.Ok(@operator);
    /// });
    /// </code>
    /// </summary>
    public static async Task<IResult> ToHttpResultAsync<T>(
        this Result<T> result,
        Func<T, Task<IResult>> onSuccess)
    {
        if (result.IsSuccess)
            return await onSuccess(result.Value);

        return MapErrorToHttp(result.Error!);
    }

    /// <summary>
    /// Match pattern for Result with HTTP responses.
    /// More functional style for complex response logic.
    ///
    /// Usage:
    /// <code>
    /// return result.MatchHttp(
    ///     onSuccess: operatorId => Results.Created($"/api/operators/{operatorId}", ...),
    ///     onNotFound: error => Results.NotFound(new { error.Message }),
    ///     onConflict: error => Results.Conflict(new { error.Message }),
    ///     onValidation: error => Results.BadRequest(new { error.Message, error.Details }),
    ///     onOtherError: error => MapErrorToHttp(error)
    /// );
    /// </code>
    /// </summary>
    public static IResult MatchHttp<T>(
        this Result<T> result,
        Func<T, IResult> onSuccess,
        Func<NotFoundError, IResult>? onNotFound = null,
        Func<ConflictError, IResult>? onConflict = null,
        Func<ValidationError, IResult>? onValidation = null,
        Func<DomainError, IResult>? onOtherError = null)
    {
        if (result.IsSuccess)
            return onSuccess(result.Value);

        return result.Error switch
        {
            NotFoundError e when onNotFound != null => onNotFound(e),
            ConflictError e when onConflict != null => onConflict(e),
            ValidationError e when onValidation != null => onValidation(e),
            _ when onOtherError != null => onOtherError(result.Error!),
            _ => MapErrorToHttp(result.Error!)
        };
    }
}
