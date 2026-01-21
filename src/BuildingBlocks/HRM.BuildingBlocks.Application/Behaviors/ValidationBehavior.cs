using FluentValidation;
using FluentValidation.Results;
using HRM.BuildingBlocks.Domain.Abstractions.Errors;
using HRM.BuildingBlocks.Domain.Abstractions.Results;
using MediatR;

namespace HRM.BuildingBlocks.Application.Behaviors;

/// <summary>
/// Pipeline behavior for validating requests using FluentValidation
/// 
/// Responsibilities:
/// - Run all registered validators for the request
/// - Collect validation errors
/// - Short-circuit pipeline if validation fails
/// - Return Result with validation errors
/// 
/// Position in Pipeline: SECOND (after Logging, before UnitOfWork)
/// - Fails fast before opening database transaction
/// - Prevents invalid data from reaching handler
/// - Reduces resource usage
/// 
/// Example:
/// <code>
/// // Validator
/// public class RegisterOperatorCommandValidator : AbstractValidator<RegisterOperatorCommand>
/// {
///     public RegisterOperatorCommandValidator()
///     {
///         RuleFor(x => x.Username).NotEmpty().MinimumLength(3);
///     }
/// }
/// 
/// // Behavior automatically runs validator
/// // If validation fails → returns Result.Failure with errors
/// // If validation succeeds → calls next()
/// </code>
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // If no validators registered → skip validation
        if (!_validators.Any())
        {
            return await next();
        }

        // Run all validators in parallel
        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        // Collect all validation failures
        var failures = validationResults
            .Where(r => !r.IsValid)
            .SelectMany(r => r.Errors)
            .ToList();

        // If validation failed → return Result.Failure
        if (failures.Any())
        {
            return CreateValidationResult<TResponse>(failures);
        }

        // Validation passed → continue pipeline
        return await next();
    }

    /// <summary>
    /// Create Result.Failure with validation errors
    /// Uses reflection to construct Result<T> type
    /// </summary>
    private static TResponse CreateValidationResult<T>(List<ValidationFailure> failures)
    {
        // Check if TResponse is Result<TValue>
        if (typeof(T).IsGenericType &&
            typeof(T).GetGenericTypeDefinition() == typeof(Result<>))
        {
            // Get the TValue type parameter
            var valueType = typeof(T).GetGenericArguments()[0];

            // Create ValidationError for each failure
            var errors = failures
                .Select(f => new ValidationError(f.PropertyName, f.ErrorMessage))
                .ToArray();

            // Call Result<TValue>.Failure(DomainError)
            var validationError = DomainError.Validation(
                "Validation.Failed",
                "One or more validation errors occurred",
                errors);

            var failureMethod = typeof(Result<>)
                .MakeGenericType(valueType)
                .GetMethod(nameof(Result<object>.Failure));

            var result = failureMethod!.Invoke(null, new object[] { validationError });

            return (TResponse)result!;
        }

        // If not Result<T>, throw exception (shouldn't happen)
        throw new ValidationException(failures);
    }
}
