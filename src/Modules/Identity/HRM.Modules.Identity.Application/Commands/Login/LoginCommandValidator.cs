using FluentValidation;

namespace HRM.Modules.Identity.Application.Commands.Login;

/// <summary>
/// Validator for LoginCommand
/// Validates input before handler execution
///
/// FluentValidation Pipeline:
/// 1. Request arrives → ValidationBehavior intercepts
/// 2. Validator runs synchronously
/// 3. If invalid → returns ValidationError with Details
/// 4. If valid → continues to handler
///
/// Validation Strategy:
/// - Keep validation simple (complex checks in handler)
/// - Don't reveal system information in error messages
/// - Username/email: Basic format/length only
/// - Password: Only presence check (no complexity here)
///
/// Security Considerations:
/// - Don't check if username exists (enumeration risk)
/// - Don't validate password complexity (already enforced at registration)
/// - Generic messages prevent information leakage
/// - Handler performs actual authentication logic
///
/// Error Response Example:
/// {
///   "code": "Validation.Failed",
///   "message": "One or more validation errors occurred",
///   "details": {
///     "UsernameOrEmail": ["Username or email is required"],
///     "Password": ["Password is required"]
///   }
/// }
/// </summary>
public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        // UsernameOrEmail validation
        RuleFor(x => x.UsernameOrEmail)
            .NotEmpty()
            .WithMessage("Username or email is required")
            .MinimumLength(3)
            .WithMessage("Username or email must be at least 3 characters")
            .MaximumLength(255)
            .WithMessage("Username or email cannot exceed 255 characters");

        // Password validation
        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required")
            .MaximumLength(255)
            .WithMessage("Password cannot exceed 255 characters");

        // RememberMe validation (optional, always valid)
        // No validation needed - it's a boolean

        // IpAddress validation (optional, basic format check)
        RuleFor(x => x.IpAddress)
            .MaximumLength(50)
            .WithMessage("IP address cannot exceed 50 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.IpAddress));

        // UserAgent validation (optional, length check)
        RuleFor(x => x.UserAgent)
            .MaximumLength(500)
            .WithMessage("User agent cannot exceed 500 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.UserAgent));
    }
}
