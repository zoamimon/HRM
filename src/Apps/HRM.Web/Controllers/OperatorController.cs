using HRM.Web.Models;
using HRM.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Web.Controllers;

/// <summary>
/// Controller for operator management
/// Handles operator registration and related operations
/// </summary>
[Authorize]
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
    /// GET: /Operator or /Operator/Index
    /// Display paginated list of operators with search and filter
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        string? searchTerm = null,
        string? status = null,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        // Validate pagination parameters
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        // Call API to get operators
        var response = await _apiClient.GetOperatorsAsync(
            searchTerm,
            status,
            pageNumber,
            pageSize,
            cancellationToken);

        var viewModel = new OperatorListViewModel
        {
            SearchTerm = searchTerm,
            StatusFilter = status,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        if (response.IsSuccess && response.Data != null)
        {
            viewModel.Operators = response.Data;
        }
        else
        {
            // Log error and show empty list with error message
            _logger.LogError("Failed to get operators: {ErrorMessage}", response.ErrorMessage);
            TempData["ErrorMessage"] = response.ErrorMessage ?? "Failed to load operators";
            viewModel.Operators = new PagedResult<OperatorSummary>();
        }

        return View(viewModel);
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
            TempData["OperatorId"] = response.Data.Id;
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
