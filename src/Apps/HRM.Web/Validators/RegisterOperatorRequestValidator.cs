using FluentValidation;
using HRM.Web.Models;

namespace HRM.Web.Validators;

/// <summary>
/// Validator for RegisterOperatorRequest
/// Ensures all fields meet business rules before calling API
/// </summary>
public sealed class RegisterOperatorRequestValidator : AbstractValidator<RegisterOperatorRequest>
{
    public RegisterOperatorRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required")
            .MinimumLength(3).WithMessage("Username must be at least 3 characters")
            .MaximumLength(50).WithMessage("Username cannot exceed 50 characters")
            .Matches(@"^[a-zA-Z0-9._-]+$").WithMessage("Username can only contain letters, numbers, dots, underscores, and hyphens");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email must be a valid email address")
            .MaximumLength(100).WithMessage("Email cannot exceed 100 characters");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one number")
            .Matches(@"[\W_]").WithMessage("Password must contain at least one special character");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Confirm Password is required")
            .Equal(x => x.Password).WithMessage("Passwords do not match");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full Name is required")
            .MinimumLength(2).WithMessage("Full Name must be at least 2 characters")
            .MaximumLength(100).WithMessage("Full Name cannot exceed 100 characters");

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+?[0-9\s\-\(\)]+$").WithMessage("Phone Number must be a valid phone number")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));
    }
}
