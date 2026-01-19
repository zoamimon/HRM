using FluentValidation;
using HRM.BuildingBlocks.Application.Abstractions.Commands;
using HRM.BuildingBlocks.Domain.Abstractions.Results;
using MediatR;

namespace HRM.BuildingBlocks.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior for validating commands using FluentValidation.
/// Runs validators before command handler execution and returns validation errors as Result.
/// 
/// Pipeline Position:
/// <code>
/// 1. LoggingBehavior (logs request)
/// 2. ValidationBehavior ← validates command
/// 3. CommandHandler (only if validation passes)
/// 4. UnitOfWorkBehavior
/// 5. LoggingBehavior (logs result)
/// </code>
/// 
/// Applied To:
/// - Commands only (implement ICommandBase)
/// - NOT queries (queries don't need validation in pipeline)
/// 
/// Why Commands Only:
/// - Commands modify state → must be valid
/// - Queries are read-only → validation optional (can be in handler)
/// - Commands return Result → can wrap validation errors
/// - Queries return direct values → validation would break signature
/// 
/// Validation Flow:
/// <code>
/// 1. Check if request is command (ICommandBase)
/// 2. If not command → skip validation, proceed to handler
/// 3. If command → find all validators for command type
/// 4. If no validators → proceed to handler
/// 5. If validators exist:
///    a. Run all validators concurrently
///    b. Collect all validation failures
///    c. If any failures → return Result.Failure with validation errors
///    d. If all pass → proceed to handler
/// </code>
/// 
/// Example Validator:
/// <code>
/// public class RegisterOperatorCommandValidator : AbstractValidator&lt;RegisterOperatorCommand&gt;
/// {
///     public RegisterOperatorCommandValidator()
///     {
///         RuleFor(x => x.Username)
///             .NotEmpty()
///             .WithMessage("Username is required")
///             .MinimumLength(3)
///             .WithMessage("Username must be at least 3 characters")
///             .MaximumLength(50)
///             .WithMessage("Username cannot exceed 50 characters")
///             .Matches("^[a-zA-Z0-9._-]+$")
///             .WithMessage("Username can only contain letters, numbers, dots, underscores, and hyphens");
///         
///         RuleFor(x => x.Email)
///             .NotEmpty()
///             .WithMessage("Email is required")
///             .EmailAddress()
///             .WithMessage("Invalid email format");
///         
///         RuleFor(x => x.Password)
///             .NotEmpty()
///             .WithMessage("Password is required")
///             .MinimumLength(8)
///             .WithMessage("Password must be at least 8 characters")
///             .Matches("[A-Z]")
///             .WithMessage("Password must contain at least one uppercase letter")
///             .Matches("[a-z]")
///             .WithMessage("Password must contain at least one lowercase letter")
///             .Matches("[0-9]")
///             .WithMessage("Password must contain at least one digit")
///             .Matches("[^a-zA-Z0-9]")
///             .WithMessage("Password must contain at least one special character");
///     }
/// }
/// </code>
/// 
/// Validation Result Example:
/// <code>
/// // Valid command:
/// var command = new RegisterOperatorCommand
/// {
///     Username = "admin",
///     Email = "admin@example.com",
///     Password = "SecurePass123!"
/// };
/// // Result: Validation passes, handler executes
/// 
/// // Invalid command:
/// var command = new RegisterOperatorCommand
/// {
///     Username = "a",              // Too short
///     Email = "invalid-email",     // Invalid format
///     Password = "weak"            // Doesn't meet requirements
/// };
/// // Result: Result.Failure with ValidationError:
/// {
///   IsSuccess: false,
///   Error: ValidationError {
///     Code: "Validation.Error",
///     Message: "One or more validation errors occurred",
///     Details: {
///       "Username": ["Username must be at least 3 characters"],
///       "Email": ["Invalid email format"],
///       "Password": [
///         "Password must be at least 8 characters",
///         "Password must contain at least one uppercase letter",
///         "Password must contain at least one digit",
///         "Password must contain at least one special character"
///       ]
///     }
///   }
/// }
///
/// // HTTP Mapping (via ResultExtensions in API layer):
/// // ValidationError → 400 Bad Request with error details
/// </code>
/// 
/// Complex Validation Example:
/// <code>
/// public class CreateDepartmentCommandValidator : AbstractValidator&lt;CreateDepartmentCommand&gt;
/// {
///     private readonly ICompanyRepository _companyRepository;
///     private readonly IDepartmentRepository _departmentRepository;
///     
///     public CreateDepartmentCommandValidator(
///         ICompanyRepository companyRepository,
///         IDepartmentRepository departmentRepository)
///     {
///         _companyRepository = companyRepository;
///         _departmentRepository = departmentRepository;
///         
///         RuleFor(x => x.Name)
///             .NotEmpty()
///             .MaximumLength(100);
///         
///         RuleFor(x => x.Code)
///             .NotEmpty()
///             .MaximumLength(20)
///             .Matches("^[A-Z0-9-]+$")
///             .WithMessage("Code must contain only uppercase letters, numbers, and hyphens");
///         
///         // Async validation: Check company exists
///         RuleFor(x => x.CompanyId)
///             .NotEmpty()
///             .MustAsync(async (companyId, cancellation) => 
///             {
///                 return await _companyRepository.ExistsByIdAsync(companyId);
///             })
///             .WithMessage("Company does not exist");
///         
///         // Async validation: Check code uniqueness
///         RuleFor(x => x)
///             .MustAsync(async (command, cancellation) => 
///             {
///                 return !await _departmentRepository.ExistsByCodeAndCompanyAsync(
///                     command.Code,
///                     command.CompanyId
///                 );
///             })
///             .WithMessage("Department code already exists in this company");
///     }
/// }
/// </code>
/// 
/// Error Aggregation:
/// All validation errors from all validators are collected:
/// 
/// <code>
/// // Multiple validators can run:
/// // - RegisterOperatorCommandValidator
/// // - CustomBusinessRuleValidator
/// // - SecurityPolicyValidator
/// 
/// // All failures aggregated into single Result:
/// Result.Failure(new ValidationError(
///     "Validation.Error",
///     "One or more validation errors occurred",
///     allValidationErrors // Dictionary&lt;string, string[]&gt;
/// ))
/// </code>
/// 
/// Performance Considerations:
/// - Validators run concurrently (ValidateAsync for each validator)
/// - Complex async validations can be slow (database checks)
/// - Consider caching for expensive validations
/// - Fail-fast in validators when possible
/// 
/// Best Practices:
/// 
/// 1. Simple Rules First:
/// <code>
/// RuleFor(x => x.Username)
///     .NotEmpty()              // Fast, fails immediately
///     .MinimumLength(3)        // Fast, fails immediately
///     .MustAsync(CheckUnique); // Slow, runs last
/// </code>
/// 
/// 2. Reusable Validators:
/// <code>
/// public class EmailValidator : AbstractValidator&lt;string&gt;
/// {
///     public EmailValidator()
///     {
///         RuleFor(x => x)
///             .NotEmpty()
///             .EmailAddress()
///             .MaximumLength(255);
///     }
/// }
/// 
/// // Use in multiple command validators:
/// RuleFor(x => x.Email).SetValidator(new EmailValidator());
/// </code>
/// 
/// 3. Custom Error Codes:
/// <code>
/// RuleFor(x => x.Username)
///     .NotEmpty()
///     .WithErrorCode("Username.Required")
///     .WithMessage("Username is required");
/// 
/// // Error result will contain custom code
/// </code>
/// 
/// Testing:
/// <code>
/// public class RegisterOperatorCommandValidatorTests
/// {
///     private readonly RegisterOperatorCommandValidator _validator;
///     
///     [Fact]
///     public async Task Validate_WhenUsernameEmpty_ShouldHaveError()
///     {
///         // Arrange
///         var command = new RegisterOperatorCommand { Username = "" };
///         
///         // Act
///         var result = await _validator.ValidateAsync(command);
///         
///         // Assert
///         result.IsValid.Should().BeFalse();
///         result.Errors.Should().Contain(e => e.PropertyName == "Username");
///     }
/// }
/// 
/// public class ValidationBehaviorTests
/// {
///     [Fact]
///     public async Task Handle_WhenValidationFails_ShouldReturnFailureResult()
///     {
///         // Arrange
///         var validator = new TestCommandValidator();
///         var behavior = new ValidationBehavior&lt;TestCommand, Result&gt;(new[] { validator });
///         
///         // Act
///         var result = await behavior.Handle(
///             new TestCommand(),
///             () => Task.FromResult(Result.Success()),
///             CancellationToken.None
///         );
///         
///         // Assert
///         result.IsFailure.Should().BeTrue();
///         result.Error.Should().BeOfType&lt;ValidationError&gt;();
///     }
/// }
/// </code>
/// 
/// Registration in DI:
/// <code>
/// // Startup.cs or Program.cs
/// services.AddValidatorsFromAssembly(typeof(RegisterOperatorCommandValidator).Assembly);
/// 
/// // MediatR automatically picks up ValidationBehavior
/// services.AddMediatR(cfg =>
/// {
///     cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
///     cfg.AddOpenBehavior(typeof(ValidationBehavior&lt;,&gt;));
/// });
/// </code>
/// </summary>
/// <typeparam name="TRequest">Type of request (must be command)</typeparam>
/// <typeparam name="TResponse">Type of response (must be Result or Result&lt;T&gt;)</typeparam>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    /// <summary>
    /// Handles validation for commands in the pipeline.
    /// Validates request before handler execution and returns validation errors as Result.
    /// </summary>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Only validate commands (ICommandBase)
        if (request is not ICommandBase)
        {
            // Not a command (probably a query), skip validation
            return await next();
        }

        // Check if any validators exist
        if (!_validators.Any())
        {
            // No validators registered, proceed to handler
            return await next();
        }

        // Run all validators concurrently
        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(request, cancellationToken))
        );

        // Collect all validation failures
        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        // If no failures, proceed to handler
        if (!failures.Any())
        {
            return await next();
        }

        // Group validation failures by property name
        var errorsDictionary = failures
            .GroupBy(f => f.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => f.ErrorMessage).ToArray()
            );

        // Create validation error using pure DomainError hierarchy
        var validationError = new ValidationError(
            "Validation.Error",
            "One or more validation errors occurred",
            errorsDictionary
        );

        // Return failure result
        return CreateValidationFailureResult(validationError);
    }

    /// <summary>
    /// Creates a validation failure result of the appropriate type.
    /// Uses reflection to call generic Result.Failure method.
    /// </summary>
    private static TResponse CreateValidationFailureResult(DomainError error)
    {
        // Handle Result (void command)
        if (typeof(TResponse) == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(error);
        }

        // Handle Result<TValue> (command returning data)
        if (typeof(TResponse).IsGenericType &&
            typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
        {
            var valueType = typeof(TResponse).GetGenericArguments()[0];
            var failureMethod = typeof(Result)
                .GetMethod(nameof(Result.Failure), 1, new[] { typeof(DomainError) })!
                .MakeGenericMethod(valueType);

            return (TResponse)failureMethod.Invoke(null, new object[] { error })!;
        }

        // Should never reach here if used correctly
        throw new InvalidOperationException(
            $"ValidationBehavior can only be used with Result or Result<T>, but was used with {typeof(TResponse).Name}"
        );
    }
}
