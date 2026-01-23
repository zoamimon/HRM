using System.ComponentModel.DataAnnotations;

namespace HRM.Web.Models;

/// <summary>
/// Login form model with client-side validation
/// Submitted to AuthController.Login action
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// Username or email address
    /// Used to identify the operator
    /// </summary>
    [Required(ErrorMessage = "Username or Email is required")]
    [Display(Name = "Username or Email")]
    [StringLength(255, MinimumLength = 3, ErrorMessage = "Username or Email must be between 3 and 255 characters")]
    public string UsernameOrEmail { get; set; } = string.Empty;

    /// <summary>
    /// Password (plaintext, will be hashed by API)
    /// </summary>
    [Required(ErrorMessage = "Password is required")]
    [Display(Name = "Password")]
    [DataType(DataType.Password)]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Password is required")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Remember Me checkbox
    /// Extends session duration (7 days → 30 days)
    /// </summary>
    [Display(Name = "Remember Me")]
    public bool RememberMe { get; set; } = false;
}
