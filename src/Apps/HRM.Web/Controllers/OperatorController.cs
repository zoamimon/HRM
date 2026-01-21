using HRM.Web.Models;
using HRM.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Web.Controllers;

/// <summary>
/// Controller for operator management
/// Handles operator registration and related operations
/// </summary>
public class OperatorController : Controller
{
    private readonly IApiClient _apiClient;
    private readonly ILogger<OperatorController> _logger;

    public OperatorController(
        IApiClient apiClient,
        ILogger<OperatorController> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    /// <summary>
    /// GET: /Operator/Register
    /// Display the operator registration form
    /// </summary>
    [HttpGet]
    public IActionResult Register()
    {
        return View(new RegisterOperatorRequest());
    }

    /// <summary>
    /// POST: /Operator/Register
    /// Process operator registration form submission
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(
        RegisterOperatorRequest model,
        CancellationToken cancellationToken)
    {
        // Server-side validation
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Call API to register operator
        var response = await _apiClient.RegisterOperatorAsync(model, cancellationToken);

        if (response.IsSuccess && response.Data != null)
        {
            // Success - redirect to success page or show success message
            TempData["SuccessMessage"] = $"Operator '{response.Data.Username}' registered successfully!";
            TempData["OperatorId"] = response.Data.OperatorId;
            return RedirectToAction(nameof(RegisterSuccess));
        }

        // Handle validation errors from API
        if (response.ValidationErrors != null)
        {
            foreach (var (field, errors) in response.ValidationErrors)
            {
                foreach (var error in errors)
                {
                    ModelState.AddModelError(field, error);
                }
            }
        }
        else
        {
            // General error message
            ModelState.AddModelError(string.Empty, response.ErrorMessage ?? "Registration failed");
        }

        return View(model);
    }

    /// <summary>
    /// GET: /Operator/RegisterSuccess
    /// Display registration success page
    /// </summary>
    [HttpGet]
    public IActionResult RegisterSuccess()
    {
        if (TempData["SuccessMessage"] == null)
        {
            // If accessed directly without registration, redirect to register form
            return RedirectToAction(nameof(Register));
        }

        ViewBag.SuccessMessage = TempData["SuccessMessage"];
        ViewBag.OperatorId = TempData["OperatorId"];
        return View();
    }
}
