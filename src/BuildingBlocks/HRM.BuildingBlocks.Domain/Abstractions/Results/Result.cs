namespace HRM.BuildingBlocks.Domain.Abstractions.Results;

/// <summary>
/// Represents the result of an operation without return value.
/// Used for commands that perform actions but don't return data.
///
/// Benefits of Result Pattern:
/// - Explicit error handling without exceptions
/// - Type-safe success/failure distinction
/// - Composable and chainable operations
/// - Clear API contracts
/// - Better performance (no exception throwing/catching)
///
/// When to Use:
/// - Command handlers that modify state (Create, Update, Delete)
/// - Operations where success/failure is the only outcome
/// - Business logic that may fail for expected reasons
///
/// When NOT to Use:
/// - Queries (use direct DTO returns instead)
/// - Infrastructure failures (use exceptions)
/// - Unexpected errors (use exceptions)
///
/// Usage Example:
/// <code>
/// public async Task&lt;Result&gt; Handle(DeleteOperatorCommand command, CancellationToken ct)
/// {
///     var @operator = await _repository.GetByIdAsync(command.OperatorId, ct);
///     if (@operator is null)
///         return Result.Failure(
///             new NotFoundError("Operator.NotFound", "Operator not found")
///         );
///
///     if (@operator.IsSystemOperator())
///         return Result.Failure(
///             new ForbiddenError("Operator.CannotDelete", "Cannot delete system operator")
///         );
///
///     _repository.Remove(@operator);
///     return Result.Success();
/// }
///
/// // In API layer:
/// var result = await mediator.Send(command);
/// return result.ToHttpResult(); // Maps DomainError to HTTP status
/// </code>
/// </summary>
public class Result
{
    /// <summary>
    /// Indicates whether the operation succeeded.
    /// True if operation completed successfully, false if it failed.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Indicates whether the operation failed.
    /// Convenience property, inverse of IsSuccess.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Error details if operation failed.
    /// Only populated when IsFailure is true.
    /// Null when IsSuccess is true.
    /// </summary>
    public DomainError? Error { get; }

    /// <summary>
    /// Protected constructor to enforce factory method usage.
    /// Validates that success results have no error and failure results have an error.
    /// </summary>
    /// <param name="isSuccess">Whether operation succeeded</param>
    /// <param name="error">Error details (must be null for success)</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when validation fails:
    /// - Success result with non-null error
    /// - Failure result with null error
    /// </exception>
    protected Result(bool isSuccess, DomainError? error)
    {
        // Validate: Success result must not have error
        if (isSuccess && error is not null)
        {
            throw new InvalidOperationException(
                "Success result cannot have an error. " +
                "Pass null for error parameter or call Result.Success()."
            );
        }

        // Validate: Failure result must have an error
        if (!isSuccess && error is null)
        {
            throw new InvalidOperationException(
                "Failure result must have an error. " +
                "Use NotFoundError/ConflictError/ValidationError/etc. or call Result.Failure(error)."
            );
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>
    /// Create a success result without return value.
    /// Used when operation succeeds but doesn't return data.
    /// </summary>
    /// <returns>Result indicating successful operation</returns>
    public static Result Success() => new(true, null);

    /// <summary>
    /// Create a failure result without return value.
    /// Used when operation fails for expected business reasons.
    /// </summary>
    /// <param name="error">Domain error explaining why operation failed</param>
    /// <returns>Result indicating failed operation</returns>
    public static Result Failure(DomainError error) => new(false, error);

    /// <summary>
    /// Create a success result with return value.
    /// Used when operation succeeds and returns data.
    /// </summary>
    /// <typeparam name="TValue">Type of value to return</typeparam>
    /// <param name="value">Value returned by successful operation</param>
    /// <returns>Result containing the value</returns>
    public static Result<TValue> Success<TValue>(TValue value)
        => new(value, true, null);

    /// <summary>
    /// Create a failure result with value type.
    /// Used when operation fails for commands that would return a value on success.
    /// </summary>
    /// <typeparam name="TValue">Type of value that would be returned on success</typeparam>
    /// <param name="error">Domain error explaining why operation failed</param>
    /// <returns>Result indicating failed operation</returns>
    public static Result<TValue> Failure<TValue>(DomainError error)
        => new(default, false, error);

    /// <summary>
    /// Match pattern for Result without return value.
    /// Executes onSuccess or onFailure action based on result state.
    /// </summary>
    /// <param name="onSuccess">Action to execute if operation succeeded</param>
    /// <param name="onFailure">Action to execute if operation failed (receives error)</param>
    public void Match(
        Action onSuccess,
        Action<DomainError> onFailure)
    {
        if (IsSuccess)
            onSuccess();
        else
            onFailure(Error!);
    }

    /// <summary>
    /// Async match pattern for Result without return value.
    /// Executes onSuccess or onFailure function based on result state.
    /// </summary>
    public async Task<TResult> Match<TResult>(
        Func<Task<TResult>> onSuccess,
        Func<DomainError, Task<TResult>> onFailure)
    {
        return IsSuccess
            ? await onSuccess()
            : await onFailure(Error!);
    }
}

/// <summary>
/// Represents the result of an operation with a return value.
/// Used for commands that return data on success (e.g., created entity ID).
///
/// Generic Constraint:
/// - TValue can be any type (value type, reference type, nullable)
///
/// Value Access Safety:
/// - Value property throws if accessed on failure result
/// - Always check IsSuccess before accessing Value
/// - Null values are allowed for nullable reference types
///
/// Usage Example:
/// <code>
/// public async Task&lt;Result&lt;Guid&gt;&gt; Handle(RegisterOperatorCommand command, CancellationToken ct)
/// {
///     // Check for duplicate username
///     if (await _repository.ExistsByUsernameAsync(command.Username, ct))
///         return Result.Failure&lt;Guid&gt;(
///             Error.Conflict(
///                 "Operator.DuplicateUsername",
///                 $"Username '{command.Username}' already exists"
///             )
///         );
///
///     // Check for duplicate email
///     if (await _repository.ExistsByEmailAsync(command.Email, ct))
///         return Result.Failure&lt;Guid&gt;(
///             Error.Conflict(
///                 "Operator.DuplicateEmail",
///                 $"Email '{command.Email}' already exists"
///             )
///         );
///
///     // Create operator (domain event raised here)
///     var hashedPassword = _passwordHasher.HashPassword(command.Password);
///     var @operator = Operator.Register(
///         command.Username,
///         command.Email,
///         hashedPassword
///     );
///
///     // Persist
///     await _repository.AddAsync(@operator, ct);
///
///     // Return created ID
///     return Result.Success(@operator.Id);
/// }
///
/// // In API controller:
/// var result = await mediator.Send(command);
/// if (result.IsFailure)
///     return BadRequest(result.Error);
/// return CreatedAtAction(nameof(GetOperator), new { id = result.Value }, null);
/// </code>
/// </summary>
/// <typeparam name="TValue">Type of value returned on success</typeparam>
public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    /// <summary>
    /// Get the value returned by successful operation.
    ///
    /// IMPORTANT SAFETY NOTES:
    /// - Only access this property if IsSuccess is true
    /// - Throws InvalidOperationException if accessed on failure result
    /// - For nullable reference types (e.g., string?), this can return null on success
    /// - For non-nullable reference types, null check is not necessary after IsSuccess check
    ///
    /// Best Practice Pattern:
    /// <code>
    /// var result = await mediator.Send(command);
    /// if (result.IsSuccess)
    /// {
    ///     var id = result.Value; // Safe - guaranteed non-null for non-nullable types
    ///     return Ok(new { Id = id });
    /// }
    /// else
    /// {
    ///     // Never access result.Value here - will throw
    ///     return BadRequest(result.Error);
    /// }
    /// </code>
    ///
    /// Anti-Pattern (DON'T DO THIS):
    /// <code>
    /// var result = await mediator.Send(command);
    /// // Dangerous - may throw if result failed!
    /// var id = result.Value;
    /// </code>
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when accessing Value on a failure result
    /// </exception>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException(
            "Cannot access Value of a failure result. " +
            "Check IsSuccess property before accessing Value. " +
            $"Error: {Error.Code} - {Error.Message}"
        );

    /// <summary>
    /// Internal constructor used by factory methods.
    /// Ensures proper encapsulation and validation through base class.
    /// </summary>
    /// <param name="value">Value to return (only meaningful if isSuccess is true)</param>
    /// <param name="isSuccess">Whether operation succeeded</param>
    /// <param name="error">Error details (must be null if isSuccess is true)</param>
    internal Result(TValue? value, bool isSuccess, DomainError? error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>
    /// Match pattern for Result with return value.
    /// Executes onSuccess or onFailure function based on result state.
    /// </summary>
    /// <typeparam name="TResult">Type of value returned by match functions</typeparam>
    /// <param name="onSuccess">Function to execute if operation succeeded (receives value)</param>
    /// <param name="onFailure">Function to execute if operation failed (receives error)</param>
    public TResult Match<TResult>(
        Func<TValue, TResult> onSuccess,
        Func<DomainError, TResult> onFailure)
    {
        return IsSuccess
            ? onSuccess(Value)
            : onFailure(Error!);
    }

    /// <summary>
    /// Async match pattern for Result with return value.
    /// Executes onSuccess or onFailure async function based on result state.
    /// </summary>
    public async Task<TResult> Match<TResult>(
        Func<TValue, Task<TResult>> onSuccess,
        Func<DomainError, Task<TResult>> onFailure)
    {
        return IsSuccess
            ? await onSuccess(Value)
            : await onFailure(Error!);
    }
}
