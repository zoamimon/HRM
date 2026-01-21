using FluentValidation;

namespace HRM.Modules.Identity.Application.Commands.RefreshToken;

/// <summary>
/// Validator for RefreshTokenCommand
/// Ensures refresh token is provided before processing
///
/// Validation Strategy:
/// - Keep it simple (complex checks in handler)
/// - Only validate presence and basic format
/// - Handler performs actual token validation
/// </summary>
public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        // RefreshToken validation
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .WithMessage("Refresh token is required")
            .MinimumLength(10)
            .WithMessage("Refresh token format is invalid")
            .MaximumLength(200)
            .WithMessage("Refresh token cannot exceed 200 characters");

        // IpAddress validation (optional)
        RuleFor(x => x.IpAddress)
            .MaximumLength(50)
            .WithMessage("IP address cannot exceed 50 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.IpAddress));

        // UserAgent validation (optional)
        RuleFor(x => x.UserAgent)
            .MaximumLength(500)
            .WithMessage("User agent cannot exceed 500 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.UserAgent));
    }
}
