using FluentValidation;

namespace HRM.Modules.Identity.Application.Commands.ActivateOperator;

/// <summary>
/// Validator for ActivateOperatorCommand
/// Validates input before handler execution
///
/// Validation Rules:
/// 1. OperatorId:
///    - Required (not empty GUID)
///    - Must be valid GUID format
///    - Existence check done in handler (better error message)
///
/// MediatR Pipeline:
/// - ValidationBehavior executes this validator before handler
/// - If validation fails: Returns 400 Bad Request with error details
/// - If validation succeeds: Proceeds to handler
///
/// Example Validation Error:
/// <code>
/// {
///   "errors": {
///     "OperatorId": ["Operator ID is required."]
///   }
/// }
/// </code>
/// </summary>
public sealed class ActivateOperatorCommandValidator : AbstractValidator<ActivateOperatorCommand>
{
    public ActivateOperatorCommandValidator()
    {
        RuleFor(x => x.OperatorId)
            .NotEmpty()
            .WithMessage("Operator ID is required.");
    }
}
