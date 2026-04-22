using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Extensions;
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Abstract base controller for Prism workflow pages.
/// Provides all the boilerplate for GET/POST handling, antiforgery, nonce validation, and PRG pattern.
/// Integrators can extend this to create their own workflow page controllers with minimal code.
/// </summary>
/// <remarks>
/// <para>
/// This controller implements the full Prism workflow pattern:
/// </para>
/// <list type="bullet">
/// <item>Handles Umbraco route-hijacking for workflow document types via Index() dispatch.</item>
/// <item>Validates antiforgery tokens and structural integrity on every POST.</item>
/// <item>Generates and verifies tamper-proof nonces to bind form submissions to their server-side field definitions.</item>
/// <item>Implements POST-Redirect-Get (PRG) pattern to prevent double-submission and preserve user input across validation failures.</item>
/// <item>Automatically collects and submits field values to the Business App workflow engine.</item>
/// </list>
/// <para>
/// Integrators override only what is domain-specific:
/// </para>
/// <list type="bullet">
/// <item><see cref="PrePopulateFields"/> to customize field pre-population (e.g., from authenticated user claims).</item>
/// <item><see cref="CreateViewModel"/> to use a custom ViewModel derived from <see cref="PrismWorkflowViewModel"/>.</item>
/// </list>
/// </remarks>
public abstract class PrismWorkflowPageController<TViewModel> : RenderController
    where TViewModel : PrismWorkflowViewModel
{
    private readonly ILogger<RenderController> _logger;
    private readonly IBusinessAppWorkflowClient _workflowClient;
    private readonly IPublishedValueFallback _publishedValueFallback;
    private readonly IAntiforgery _antiforgery;
    private readonly IWorkflowStepNonceService _nonceService;
    private readonly IWorkflowFieldValidator _fieldValidator;

    /// <summary>
    /// Initializes a new instance of the PrismWorkflowPageController class.
    /// </summary>
    /// <param name="logger">Logger for workflow request diagnostics and warnings.</param>
    /// <param name="compositeViewEngine">Umbraco's view engine for rendering templates.</param>
    /// <param name="umbracoContextAccessor">Accessor for the current Umbraco context and published content.</param>
    /// <param name="workflowClient">Client for communicating with the Business App workflow engine.</param>
    /// <param name="publishedValueFallback">Umbraco helper for retrieving published property values with fallback support.</param>
    /// <param name="antiforgery">Service for validating antiforgery tokens on form submissions.</param>
    /// <param name="nonceService">Service for creating and resolving tamper-proof nonces bound to field definitions.</param>
    /// <param name="fieldValidator">Service for validating submitted field values against their server-side definitions.</param>
    protected PrismWorkflowPageController(
        ILogger<RenderController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        IBusinessAppWorkflowClient workflowClient,
        IPublishedValueFallback publishedValueFallback,
        IAntiforgery antiforgery,
        IWorkflowStepNonceService nonceService,
        IWorkflowFieldValidator fieldValidator)
        : base(logger, compositeViewEngine, umbracoContextAccessor)
    {
        _logger = logger;
        _workflowClient = workflowClient;
        _publishedValueFallback = publishedValueFallback;
        _antiforgery = antiforgery;
        _nonceService = nonceService;
        _fieldValidator = fieldValidator;
    }

    /// <summary>
    /// Routes GET and POST requests for the workflow page based on the HTTP method.
    /// GET requests retrieve the current workflow state and render the form.
    /// POST requests validate submitted fields, advance the workflow, and redirect using the PRG pattern.
    /// </summary>
    /// <returns>An <see cref="IActionResult"/> containing the rendered view or redirect.</returns>
    public override IActionResult Index()
    {
        if (HttpContext.Request.Method == HttpMethods.Post)
            return HandlePost().GetAwaiter().GetResult();

        return HandleGet().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Handles GET requests: retrieves the current workflow state from the Business App, 
    /// pre-populates fields if needed, generates a tamper-proof nonce, and renders the form.
    /// </summary>
    /// <returns>An <see cref="IActionResult"/> containing the rendered view with the current workflow state, or an error view if initialization fails.</returns>
    private async Task<IActionResult> HandleGet()
    {
        var workflowKey = CurrentPage!.Value<string>(_publishedValueFallback, "workflowKey") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(workflowKey))
        {
            return CurrentTemplate(ErrorViewModel(workflowKey,
                "No workflow key configured on this page. Set the 'workflowKey' property in the backoffice."));
        }

        var problems = PopProblemsFromTempData();
        var formValues = PopFormValuesFromTempData();

        // Read query parameters for instanceId and action
        var instanceId = HttpContext.Request.Query["instanceId"].ToString();
        var action = HttpContext.Request.Query["action"].ToString();

        var envelope = await _workflowClient.GetCurrentAsync(workflowKey, 
            string.IsNullOrEmpty(instanceId) ? null : instanceId,
            string.IsNullOrEmpty(action) ? null : action);

        if (envelope.ResponseState == "error")
        {
            var msg = envelope.Problems.FirstOrDefault()?.Message
                ?? $"Could not start workflow '{workflowKey}'. Is the Business App running?";
            return CurrentTemplate(ErrorViewModel(workflowKey, msg));
        }

        // Handle instance_picker response
        if (envelope.ResponseState == "instance_picker")
        {
            var vm = CreateViewModel(envelope, workflowKey, problems, formValues);
            vm.ShowInstancePicker = true;
            vm.StateDisplayName = envelope.Render?.StateDisplayName ?? workflowKey;
            return CurrentTemplate(vm);
        }

        // Allow subclasses to customize field pre-population
        var updatedEnvelope = PrePopulateFields(envelope);

        // Collect fields for nonce caching.
        // Check-answers is a read-only summary — it has no fields to validate on POST.
        var stepType = updatedEnvelope.Render?.StepType ?? string.Empty;
        var nonceFields = stepType == "check-answers"
            ? new List<FieldRenderPayload>()
            : updatedEnvelope.Render?.Components
                .SelectMany(c => c.Fields)
                .ToList() ?? new List<FieldRenderPayload>();

        var nonce = await _nonceService.CreateAsync(nonceFields);
        var vm2 = CreateViewModel(updatedEnvelope, workflowKey, problems, formValues);
        vm2.Nonce = nonce;
        return CurrentTemplate(vm2);
    }

    /// <summary>
    /// Handles POST requests: validates antiforgery tokens, verifies the nonce, 
    /// validates submitted field values, advances the workflow in the Business App, 
    /// and redirects to maintain the PRG pattern.
    /// </summary>
    /// <returns>A redirect response to the safe return URL. Validation failures are stored in TempData and displayed on the next GET.</returns>
    private async Task<IActionResult> HandlePost()
    {
        // Manual antiforgery check
        try
        {
            await _antiforgery.ValidateRequestAsync(HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            _logger.LogWarning("Workflow POST: antiforgery validation failed");
            return BadRequest("Invalid form submission.");
        }

        var form = HttpContext.Request.Form;

        // Nonce validation — tamper-proofing
        var nonce = form["Nonce"].ToString();
        var returnUrl = form["ReturnUrl"].ToString();
        var safeReturnUrl = GetSafeReturnUrl(returnUrl);

        if (string.IsNullOrEmpty(nonce))
        {
            _logger.LogWarning("Workflow POST: missing nonce — possible form tampering");
            return Redirect(safeReturnUrl);
        }

        var authoritativeFields = await _nonceService.ResolveAsync(nonce);
        if (authoritativeFields == null)
        {
            _logger.LogWarning("Workflow POST: nonce expired or invalid — redirecting to GET");
            return Redirect(safeReturnUrl);
        }

        // Structural validation
        var submittedFields = form.Keys
            .Where(k => k.StartsWith("fields[", StringComparison.Ordinal) && k.EndsWith("]"))
            .ToDictionary(
                k => k[7..^1],
                k => form[k].ToString());

        var validationResult = _fieldValidator.Validate(authoritativeFields, submittedFields);
        if (!validationResult.IsValid)
        {
            var problems = validationResult.Errors
                .Select(e => new WorkflowProblem { FieldKey = e.Key, Message = e.Value, Code = "validation_error" })
                .ToList();
            TempData["WorkflowProblems"] = JsonSerializer.Serialize(problems);
            TempData["WorkflowFormValues"] = JsonSerializer.Serialize(submittedFields);
            return Redirect(safeReturnUrl);
        }

        var instanceId = form["InstanceId"].ToString();
        var workflowKey = form["WorkflowKey"].ToString();
        var action = form["Action"].ToString();
        var stateVersion = int.TryParse(form["StateVersion"], out var sv) ? sv : 0;

        var fieldValues = submittedFields.ToDictionary(
            kvp => kvp.Key,
            kvp => (object?)kvp.Value);

        // Combine date-input parts into a display value
        foreach (var field in authoritativeFields.Where(f => f.FieldType.Equals("date-input", StringComparison.OrdinalIgnoreCase)))
        {
            if (fieldValues.TryGetValue($"{field.FieldKey}-day", out var day) &&
                fieldValues.TryGetValue($"{field.FieldKey}-month", out var month) &&
                fieldValues.TryGetValue($"{field.FieldKey}-year", out var year) &&
                !string.IsNullOrWhiteSpace(day?.ToString()) &&
                !string.IsNullOrWhiteSpace(month?.ToString()) &&
                !string.IsNullOrWhiteSpace(year?.ToString()))
            {
                fieldValues[field.FieldKey] = $"{day}/{month}/{year}";
            }
        }

        if (string.IsNullOrEmpty(instanceId) || string.IsNullOrEmpty(action))
        {
            _logger.LogWarning("Workflow POST: missing InstanceId or Action");
            return Redirect(safeReturnUrl);
        }

        var envelope = await _workflowClient.AdvanceAsync(
            workflowKey, instanceId, action, stateVersion, fieldValues);

        if (envelope.Problems.Count > 0)
        {
            TempData["WorkflowProblems"] = JsonSerializer.Serialize(envelope.Problems);
            TempData["WorkflowFormValues"] = JsonSerializer.Serialize(submittedFields);
        }

        return Redirect(safeReturnUrl);
    }

    /// <summary>
    /// Override this method to customize field pre-population based on authenticated user context or external data.
    /// </summary>
    /// <param name="envelope">The <see cref="WorkflowResponseEnvelope"/> containing the current workflow state and fields to render.</param>
    /// <returns>
    /// The modified envelope with field values pre-populated. 
    /// Default implementation returns the envelope unchanged. 
    /// Implementations should modify field default values or prefilled data within the envelope's field groups.
    /// </returns>
    /// <remarks>
    /// This method is called after the workflow engine returns the current state but before nonce generation.
    /// Use it to populate fields based on:
    /// <list type="bullet">
    /// <item>Authenticated user claims (name, email, organization, etc.)</item>
    /// <item>Previous workflow instances</item>
    /// <item>External data sources or APIs</item>
    /// <item>Session or request context</item>
    /// </list>
    /// The modified envelope's field values will be included in the nonce, protecting them from tampering.
    /// </remarks>
    protected virtual WorkflowResponseEnvelope PrePopulateFields(WorkflowResponseEnvelope envelope)
    {
        return envelope;
    }

    /// <summary>
    /// Override this method to use a custom ViewModel derived from <see cref="PrismWorkflowViewModel"/>.
    /// </summary>
    /// <param name="envelope">The <see cref="WorkflowResponseEnvelope"/> from the Business App containing the current workflow state.</param>
    /// <param name="workflowKey">The workflow definition key read from the Umbraco page property.</param>
    /// <param name="problems">Validation problems from the previous POST round-trip, or null if this is the initial GET.</param>
    /// <param name="formValues">Pre-filled form values to repopulate the form after validation failure, or null if no prior submission.</param>
    /// <returns>
    /// A new instance of <typeparamref name="TViewModel"/> initialized with all properties from the envelope and parameters.
    /// Default implementation creates a base <see cref="PrismWorkflowViewModel"/> instance.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when the ViewModel instance cannot be created via reflection.</exception>
    /// <remarks>
    /// This is called on every GET request and on form validation failures before rendering.
    /// Custom implementations should populate additional domain-specific properties 
    /// (e.g., user profile data, localized labels, feature flags) 
    /// by deriving from <see cref="PrismWorkflowViewModel"/> and reading from the protected CurrentPage context.
    /// </remarks>
    protected virtual TViewModel CreateViewModel(
        WorkflowResponseEnvelope envelope,
        string workflowKey,
        IReadOnlyList<WorkflowProblem>? problems = null,
        IReadOnlyDictionary<string, string>? formValues = null)
    {
        var render = envelope.Render;
        var vm = Activator.CreateInstance(typeof(TViewModel), CurrentPage!, _publishedValueFallback) as TViewModel
            ?? throw new InvalidOperationException($"Cannot create instance of {typeof(TViewModel).Name}");

        vm.InstanceId = envelope.InstanceId;
        vm.StateVersion = envelope.StateVersion;
        vm.WorkflowKey = workflowKey;
        vm.ReturnUrl = HttpContext.Request.PathBase + HttpContext.Request.Path;
        vm.StepType = render?.StepType ?? string.Empty;
        vm.StateDisplayName = render?.StateDisplayName ?? string.Empty;
        vm.Components = render?.Components ?? Array.Empty<PrismComponentRenderPayload>();
        vm.AvailableActions = render?.AvailableActions ?? Array.Empty<WorkflowAction>();
        vm.Problems = problems ?? Array.Empty<WorkflowProblem>();
        vm.FormValues = formValues ?? new Dictionary<string, string>();
        vm.WaitingConfig = render?.WaitingConfig;
        vm.PollAfterMs = envelope.PollAfterMs;

        return vm;
    }

    /// <summary>
    /// Creates an error view model when the workflow cannot be initialized or an unexpected error occurs.
    /// </summary>
    /// <param name="workflowKey">The workflow definition key that failed to load.</param>
    /// <param name="message">A developer-friendly error message explaining what went wrong (e.g., definition not found, Business App unreachable).</param>
    /// <returns>A ViewModel with <see cref="PrismWorkflowViewModel.HasError"/> set to true and the error message populated.</returns>
    private TViewModel ErrorViewModel(string workflowKey, string message)
    {
        var vm = Activator.CreateInstance(typeof(TViewModel), CurrentPage!, _publishedValueFallback) as TViewModel
            ?? throw new InvalidOperationException($"Cannot create instance of {typeof(TViewModel).Name}");

        vm.WorkflowKey = workflowKey;
        vm.HasError = true;
        vm.ErrorMessage = message;
        vm.ReturnUrl = HttpContext.Request.PathBase + HttpContext.Request.Path;

        return vm;
    }

    private IReadOnlyList<WorkflowProblem> PopProblemsFromTempData()
    {
        if (TempData.TryGetValue("WorkflowProblems", out var raw) && raw is string json)
        {
            try
            {
                return JsonSerializer.Deserialize<List<WorkflowProblem>>(json)
                    ?? (IReadOnlyList<WorkflowProblem>)Array.Empty<WorkflowProblem>();
            }
            catch
            {
                // ignore deserialization failures
            }
        }

        return Array.Empty<WorkflowProblem>();
    }

    private IReadOnlyDictionary<string, string> PopFormValuesFromTempData()
    {
        if (TempData.TryGetValue("WorkflowFormValues", out var raw) && raw is string json)
        {
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                    ?? (IReadOnlyDictionary<string, string>)new Dictionary<string, string>();
            }
            catch
            {
                // ignore deserialization failures
            }
        }

        return new Dictionary<string, string>();
    }

    private string GetSafeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return "/";

        if (Url.IsLocalUrl(returnUrl))
            return returnUrl;

        _logger.LogWarning("Rejected external ReturnUrl in workflow POST: {ReturnUrl}", returnUrl);
        return "/";
    }
}
