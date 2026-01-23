using System.Text.RegularExpressions;
using FluentValidation;

namespace HRM.Modules.Identity.Application.Commands.RegisterOperator;

/// <summary>
/// Validator for RegisterOperatorCommand
/// Validates input before handler execution
///
/// Validation Rules:
/// 1. Username:
///    - Required
///    - Length: 3-50 characters
///    - Format: Alphanumeric with underscores and hyphens only
///    - Examples: john_doe, john-doe, john123
///
/// 2. Email:
///    - Required
///    - Valid email format (RFC 5322 via FluentValidation)
///    - Max length: 255 characters
///    - No uniqueness check here (done in handler for better error messages)
///
/// 3. Password:
///    - Required
///    - Min length: 12 characters (strong password policy)
///    - Must contain:
///      * At least one uppercase letter (A-Z)
///      * At least one lowercase letter (a-z)
///      * At least one digit (0-9)
///      * At least one special character (!@#$%^&*()_+-=[]{}|;:,.<>?)
///
/// 4. FullName:
///    - Required
///    - Length: 1-200 characters
///    - No format restrictions (supports international names)
///
/// 5. PhoneNumber:
///    - Optional
///    - If provided: Basic format validation (10-15 digits with optional +)
///    - E.164 format recommended: +[country code][number]
///
/// MediatR Pipeline:
/// - ValidationBehavior executes this validator before handler
/// - If validation fails: Returns 400 Bad Request with error details
/// - If validation succeeds: Proceeds to handler
///
/// Example Validation Errors:
/// <code>
/// {
///   "errors": {
///     "Username": ["Username must be between 3 and 50 characters"],
///     "Password": ["Password must contain at least one uppercase letter"]
///   }
/// }
/// </code>
/// </summary>
public sealed class RegisterOperatorCommandValidator : AbstractValidator<RegisterOperatorCommand>
{
    // Username: Alphanumeric with underscores and hyphens
    private static readonly Regex UsernameRegex = new(@"^[a-zA-Z0-9_-]+$", RegexOptions.Compiled);

    // Password complexity: At least one of each character type
    private static readonly Regex PasswordUppercaseRegex = new(@"[A-Z]", RegexOptions.Compiled);
    private static readonly Regex PasswordLowercaseRegex = new(@"[a-z]", RegexOptions.Compiled);
    private static readonly Regex PasswordDigitRegex = new(@"[0-9]", RegexOptions.Compiled);
    private static readonly Regex PasswordSpecialCharRegex = new(@"[!@#$%^&*()_+\-=\[\]{}|;:,.<>?]", RegexOptions.Compiled);

    // Phone number: Basic validation (10-15 digits with optional + prefix)
    private static readonly Regex PhoneNumberRegex = new(@"^\+?[0-9]{10,15}$", RegexOptions.Compiled);

    public RegisterOperatorCommandValidator()
    {
        // Username validation
        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage("Username is required.")
            .Length(3, 50)
            .WithMessage("Username must be between 3 and 50 characters.")
            .Matches(UsernameRegex)
            .WithMessage("Username can only contain letters, numbers, underscores, and hyphens.");

        // Email validation
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required.")
            .EmailAddress()
            .WithMessage("Email address format is invalid.")
            .MaximumLength(255)
            .WithMessage("Email address cannot exceed 255 characters.");

        // Password validation
        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required.")
            .MinimumLength(12)
            .WithMessage("Password must be at least 12 characters long.")
            .Must(HaveUppercase)
            .WithMessage("Password must contain at least one uppercase letter (A-Z).")
            .Must(HaveLowercase)
            .WithMessage("Password must contain at least one lowercase letter (a-z).")
            .Must(HaveDigit)
            .WithMessage("Password must contain at least one digit (0-9).")
            .Must(HaveSpecialCharacter)
            .WithMessage("Password must contain at least one special character (!@#$%^&*()_+-=[]{}|;:,.<>?).");

        // FullName validation
        RuleFor(x => x.FullName)
            .NotEmpty()
            .WithMessage("Full name is required.")
            .Length(1, 200)
            .WithMessage("Full name must be between 1 and 200 characters.");

        // PhoneNumber validation (optional)
        RuleFor(x => x.PhoneNumber)
            .Matches(PhoneNumberRegex)
            .WithMessage("Phone number must be in valid format (10-15 digits, optional + prefix).")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));
    }

    /// <summary>
    /// Check if password contains at least one uppercase letter
    /// </summary>
    private static bool HaveUppercase(string password)
    {
        return PasswordUppercaseRegex.IsMatch(password);
    }

    /// <summary>
    /// Check if password contains at least one lowercase letter
    /// </summary>
    private static bool HaveLowercase(string password)
    {
        return PasswordLowercaseRegex.IsMatch(password);
    }

    /// <summary>
    /// Check if password contains at least one digit
    /// </summary>
    private static bool HaveDigit(string password)
    {
        return PasswordDigitRegex.IsMatch(password);
    }

    /// <summary>
    /// Check if password contains at least one special character
    /// </summary>
    private static bool HaveSpecialCharacter(string password)
    {
        return PasswordSpecialCharRegex.IsMatch(password);
    }
}
