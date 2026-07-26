using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Extensions;
using UmbracoPrism.Core.Models.ServiceDesign;
using UmbracoPrism.Core.Services;
using UmbracoPrism.Core.Services.ServiceDesign;
using UmbracoPrism.Shared.Models.ServiceDesign;

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
/// <item><see cref="CreateViewModel"/> to use a custom ViewModel derived from <see cref="PrismServiceRequestViewModel"/>.</item>
/// </list>
/// </remarks>
public abstract class PrismServiceRequestPageController<TViewModel> : RenderController
    where TViewModel : PrismServiceRequestViewModel
{
    /// <summary>Fallback max upload size for a <c>file-upload</c> field with no <c>MaxSizeBytes</c> of its own.</summary>
    public const long DefaultMaxFileSizeBytes = 10 * 1024 * 1024;

    private readonly ILogger<RenderController> _logger;
    private readonly IBusinessAppProcessManagerClient _processManagerClient;
    private readonly IPublishedValueFallback _publishedValueFallback;
    private readonly IAntiforgery _antiforgery;
    private readonly ITouchpointNonceService _nonceService;
    private readonly IServiceRequestFieldValidator _fieldValidator;
    private readonly IServiceRequestFileStorage _fileStorage;
    private readonly IUploadTokenService _uploadTokenService;

    /// <summary>
    /// Initializes a new instance of the PrismServiceRequestPageController class.
    /// </summary>
    /// <param name="logger">Logger for workflow request diagnostics and warnings.</param>
    /// <param name="compositeViewEngine">Umbraco's view engine for rendering templates.</param>
    /// <param name="umbracoContextAccessor">Accessor for the current Umbraco context and published content.</param>
    /// <param name="workflowClient">Client for communicating with the Business App workflow engine.</param>
    /// <param name="publishedValueFallback">Umbraco helper for retrieving published property values with fallback support.</param>
    /// <param name="antiforgery">Service for validating antiforgery tokens on form submissions.</param>
    /// <param name="nonceService">Service for creating and resolving tamper-proof nonces bound to field definitions.</param>
    /// <param name="fieldValidator">Service for validating submitted field values against their server-side definitions.</param>
    /// <param name="fileStorage">Service for persisting files posted against <c>file-upload</c> fields.</param>
    /// <param name="uploadTokenService">Resolves a field that was already uploaded asynchronously ahead of this submission.</param>
    protected PrismServiceRequestPageController(
        ILogger<RenderController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        IBusinessAppProcessManagerClient workflowClient,
        IPublishedValueFallback publishedValueFallback,
        IAntiforgery antiforgery,
        ITouchpointNonceService nonceService,
        IServiceRequestFieldValidator fieldValidator,
        IServiceRequestFileStorage fileStorage,
        IUploadTokenService uploadTokenService)
        : base(logger, compositeViewEngine, umbracoContextAccessor)
    {
        _logger = logger;
        _processManagerClient = workflowClient;
        _publishedValueFallback = publishedValueFallback;
        _antiforgery = antiforgery;
        _nonceService = nonceService;
        _fieldValidator = fieldValidator;
        _fileStorage = fileStorage;
        _uploadTokenService = uploadTokenService;
    }

    /// <summary>
    /// Whether this workflow page requires an authenticated Prism Member. Defaults to
    /// <see langword="true"/> — the member-authenticated business-workflow demo pattern every
    /// existing subclass relies on. Override to <see langword="false"/> for an anonymous-first
    /// public journey (e.g. a GDS-style CMS Workflow page): the workflow itself still resolves
    /// its own notion of "who this is" (a member's identity when logged in, an anonymous
    /// session identity otherwise) — this flag only controls whether an unauthenticated visitor
    /// gets redirected to login before ever reaching the page.
    /// </summary>
    protected virtual bool RequiresAuthentication => true;

    /// <summary>
    /// Routes GET and POST requests for the workflow page based on the HTTP method.
    /// GET requests retrieve the current workflow state and render the form.
    /// POST requests validate submitted fields, advance the workflow, and redirect using the PRG pattern.
    /// </summary>
    /// <returns>An <see cref="IActionResult"/> containing the rendered view or redirect.</returns>
    public override IActionResult Index()
    {
        if (RequiresAuthentication && User.Identity?.IsAuthenticated != true)
        {
            return Redirect(BuildLoginRedirectUrl());
        }

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
        var blueprintKey = CurrentPage!.Value<string>(_publishedValueFallback, "blueprintKey") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(blueprintKey))
        {
            return CurrentTemplate(ErrorViewModel(blueprintKey,
                "No workflow key configured on this page. Set the 'blueprintKey' property in the backoffice."));
        }

        var problems = PopProblemsFromTempData();
        var formValues = PopFormValuesFromTempData();

        // Read query parameters for instanceId and action
        var instanceId = HttpContext.Request.Query["instanceId"].ToString();
        var action = HttpContext.Request.Query["action"].ToString();

        var envelope = await _processManagerClient.GetCurrentAsync(blueprintKey, 
            string.IsNullOrEmpty(instanceId) ? null : instanceId,
            string.IsNullOrEmpty(action) ? null : action);

        if (envelope.ResponseState == "error")
        {
            var msg = envelope.Problems.FirstOrDefault()?.Message
                ?? $"Could not start workflow '{blueprintKey}'. Is the Business App running?";
            return CurrentTemplate(ErrorViewModel(blueprintKey, msg));
        }

        // Handle instance_picker response
        if (envelope.ResponseState == "instance_picker")
        {
            var vm = CreateViewModel(envelope, blueprintKey, problems, formValues);
            vm.ShowInstancePicker = true;
            vm.StateDisplayName = envelope.Render?.StateDisplayName ?? blueprintKey;
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
        var vm2 = CreateViewModel(updatedEnvelope, blueprintKey, problems, formValues);
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
        // Read here (rather than after validation, where the other hidden fields are read) —
        // resolving an async-uploaded file's token below needs it too.
        var instanceId = form["InstanceId"].ToString();

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

        // Files never appear in form.Keys (they're a separate IFormFileCollection) — a
        // file-upload field's "value" for required-checking purposes is simply whether a file
        // was posted for it, so validate against a copy carrying a non-empty sentinel and let
        // the existing string-based required check apply completely unmodified. The sentinel
        // never enters submittedFields itself — nothing has actually been saved yet at this
        // point, so it must not leak into the redisplay-on-failure form values.
        var postedFiles = authoritativeFields
            .Where(field => field.FieldType.Equals("file-upload", StringComparison.OrdinalIgnoreCase))
            .Select(field => (Field: field, File: form.Files.GetFile($"fields[{field.FieldKey}]")))
            .Where(pair => pair.File is not null)
            .ToList();

        // A file-upload field with no raw posted file may instead carry the opaque token
        // CmsServiceRequestFileUploadController issued when the visitor's browser uploaded it
        // asynchronously ahead of this submission (prism-file-upload.ts) — resolve those here.
        // A token is only trusted if it resolves at all AND its cached binding names this exact
        // instance/field; anything else (expired, forged, copied from a different field or a
        // different visitor's instance) is treated as if nothing were submitted at all by
        // clearing it out of submittedFields, so the ordinary required-check below catches it
        // the same way a genuinely-missing file would be caught.
        var postedFileKeys = postedFiles.Select(pair => pair.Field.FieldKey).ToHashSet(StringComparer.Ordinal);
        var tokenUploads = new List<(FieldRenderPayload Field, UploadTokenBinding Binding)>();
        var validationOverrides = new Dictionary<string, string>();
        foreach (var field in authoritativeFields.Where(f =>
            f.FieldType.Equals("file-upload", StringComparison.OrdinalIgnoreCase) && !postedFileKeys.Contains(f.FieldKey)))
        {
            if (!submittedFields.TryGetValue(field.FieldKey, out var token) || string.IsNullOrWhiteSpace(token))
            {
                // Nothing submitted for this field at all this time round — a resubmit of a
                // stage the visitor already satisfied earlier (e.g. editing a different field
                // and continuing again) with no new choice made for THIS one. field.Value
                // already reflects the instance's existing stored reference (the same value the
                // "Uploaded: …" display state renders from), so let it satisfy the required
                // check without touching fieldValues — leaving this key out of fieldValues
                // entirely means the engine's own merge just preserves whatever's already there,
                // the same way any other untouched field from an earlier stage is preserved.
                if (!string.IsNullOrWhiteSpace(field.Value?.ToString()))
                {
                    validationOverrides[field.FieldKey] = field.Value!.ToString()!;
                }
                continue;
            }

            var binding = await _uploadTokenService.ResolveAsync(token);
            if (binding is not null
                && string.Equals(binding.InstanceId, instanceId, StringComparison.Ordinal)
                && string.Equals(binding.FieldKey, field.FieldKey, StringComparison.Ordinal))
            {
                tokenUploads.Add((field, binding));
            }
            else
            {
                submittedFields[field.FieldKey] = string.Empty;
            }
        }

        var validationInput = new Dictionary<string, string>(submittedFields);
        foreach (var (field, _) in postedFiles)
        {
            validationInput[field.FieldKey] = "uploaded";
        }
        foreach (var (fieldKey, value) in validationOverrides)
        {
            validationInput[fieldKey] = value;
        }

        var validationResult = _fieldValidator.Validate(authoritativeFields, validationInput);
        var errors = new Dictionary<string, string>(validationResult.Errors);

        foreach (var (field, file) in postedFiles)
        {
            var maxSizeBytes = field.MaxSizeBytes ?? DefaultMaxFileSizeBytes;
            if (file!.Length > maxSizeBytes)
            {
                errors[field.FieldKey] = $"{field.Label} must be smaller than {maxSizeBytes / (1024 * 1024)}MB.";
            }
        }

        if (errors.Count > 0)
        {
            var problems = errors
                .Select(e => new ServiceRequestProblem { FieldKey = e.Key, Message = e.Value, Code = "validation_error" })
                .ToList();
            TempData["ServiceRequestProblems"] = JsonSerializer.Serialize(problems);
            TempData["WorkflowFormValues"] = JsonSerializer.Serialize(submittedFields);
            return Redirect(safeReturnUrl);
        }

        var blueprintKey = form["BlueprintKey"].ToString();
        var action = form["Action"].ToString();
        var stateVersion = int.TryParse(form["StateVersion"], out var sv) ? sv : 0;

        var fieldValues = submittedFields.ToDictionary(
            kvp => kvp.Key,
            kvp => (object?)kvp.Value);

        // Validation passed, so every posted file is genuinely wanted — save it now and
        // replace its sentinel with the real reference the engine will persist.
        foreach (var (field, file) in postedFiles)
        {
            fieldValues[field.FieldKey] = await _fileStorage.SaveAsync(instanceId, field.FieldKey, file!);
        }

        // Already saved by the async upload endpoint — just swap the token for the real reference.
        foreach (var (field, binding) in tokenUploads)
        {
            fieldValues[field.FieldKey] = binding.Reference;
        }

        // Combine GDS date sub-input parts (-day/-month/-year) into a display value
        foreach (var field in authoritativeFields.Where(f => f.FieldType.Equals("date", StringComparison.OrdinalIgnoreCase)))
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

        // Action can legitimately be "" — a route with a blank trigger is still a real, valid
        // action key (ProcessManagerEngine defaults a blank trigger to "continue" when building
        // available actions, so this shouldn't happen post-fix, but older/unrefreshed definitions
        // can still submit ""). What actually indicates a malformed request is the field being
        // absent from the form entirely, not merely empty.
        if (string.IsNullOrEmpty(instanceId) || !form.ContainsKey("Action"))
        {
            _logger.LogWarning("Workflow POST: missing InstanceId or Action");
            return Redirect(safeReturnUrl);
        }

        var envelope = await _processManagerClient.AdvanceAsync(
            blueprintKey, instanceId, action, stateVersion, fieldValues);

        if (envelope.Problems.Count > 0)
        {
            TempData["ServiceRequestProblems"] = JsonSerializer.Serialize(envelope.Problems);
            TempData["WorkflowFormValues"] = JsonSerializer.Serialize(submittedFields);
        }

        return Redirect(safeReturnUrl);
    }

    /// <summary>
    /// Override this method to customize field pre-population based on authenticated user context or external data.
    /// </summary>
    /// <param name="envelope">The <see cref="ServiceRequestResponseEnvelope"/> containing the current workflow state and fields to render.</param>
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
    protected virtual ServiceRequestResponseEnvelope PrePopulateFields(ServiceRequestResponseEnvelope envelope)
    {
        return envelope;
    }

    /// <summary>
    /// Override this method to use a custom ViewModel derived from <see cref="PrismServiceRequestViewModel"/>.
    /// </summary>
    /// <param name="envelope">The <see cref="ServiceRequestResponseEnvelope"/> from the Business App containing the current workflow state.</param>
    /// <param name="blueprintKey">The workflow definition key read from the Umbraco page property.</param>
    /// <param name="problems">Validation problems from the previous POST round-trip, or null if this is the initial GET.</param>
    /// <param name="formValues">Pre-filled form values to repopulate the form after validation failure, or null if no prior submission.</param>
    /// <returns>
    /// A new instance of <typeparamref name="TViewModel"/> initialized with all properties from the envelope and parameters.
    /// Default implementation creates a base <see cref="PrismServiceRequestViewModel"/> instance.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when the ViewModel instance cannot be created via reflection.</exception>
    /// <remarks>
    /// This is called on every GET request and on form validation failures before rendering.
    /// Custom implementations should populate additional domain-specific properties 
    /// (e.g., user profile data, localized labels, feature flags) 
    /// by deriving from <see cref="PrismServiceRequestViewModel"/> and reading from the protected CurrentPage context.
    /// </remarks>
    protected virtual TViewModel CreateViewModel(
        ServiceRequestResponseEnvelope envelope,
        string blueprintKey,
        IReadOnlyList<ServiceRequestProblem>? problems = null,
        IReadOnlyDictionary<string, string>? formValues = null)
    {
        var render = envelope.Render;
        var vm = Activator.CreateInstance(typeof(TViewModel), CurrentPage!, _publishedValueFallback) as TViewModel
            ?? throw new InvalidOperationException($"Cannot create instance of {typeof(TViewModel).Name}");

        vm.InstanceId = envelope.InstanceId;
        vm.StateVersion = envelope.StateVersion;
        vm.BlueprintKey = blueprintKey;
        vm.ReturnUrl = HttpContext.Request.PathBase + HttpContext.Request.Path;
        vm.StepType = render?.StepType ?? string.Empty;
        vm.StateDisplayName = render?.StateDisplayName ?? string.Empty;
        vm.Components = render?.Components ?? Array.Empty<PrismComponentRenderPayload>();
        vm.AvailableActions = render?.AvailableActions ?? Array.Empty<ServiceRequestAction>();
        vm.Problems = problems ?? Array.Empty<ServiceRequestProblem>();
        vm.FormValues = formValues ?? new Dictionary<string, string>();
        vm.PollAfterMs = envelope.PollAfterMs;
        vm.LiveModelJson = render?.Data?["live"]?.ToJsonString();

        return vm;
    }

    /// <summary>
    /// Creates an error view model when the workflow cannot be initialized or an unexpected error occurs.
    /// </summary>
    /// <param name="blueprintKey">The workflow definition key that failed to load.</param>
    /// <param name="message">A developer-friendly error message explaining what went wrong (e.g., definition not found, Business App unreachable).</param>
    /// <returns>A ViewModel with <see cref="PrismServiceRequestViewModel.HasError"/> set to true and the error message populated.</returns>
    private TViewModel ErrorViewModel(string blueprintKey, string message)
    {
        var vm = Activator.CreateInstance(typeof(TViewModel), CurrentPage!, _publishedValueFallback) as TViewModel
            ?? throw new InvalidOperationException($"Cannot create instance of {typeof(TViewModel).Name}");

        vm.BlueprintKey = blueprintKey;
        vm.HasError = true;
        vm.ErrorMessage = message;
        vm.ReturnUrl = HttpContext.Request.PathBase + HttpContext.Request.Path;

        return vm;
    }

    private IReadOnlyList<ServiceRequestProblem> PopProblemsFromTempData()
    {
        if (TempData.TryGetValue("ServiceRequestProblems", out var raw) && raw is string json)
        {
            try
            {
                return JsonSerializer.Deserialize<List<ServiceRequestProblem>>(json)
                    ?? (IReadOnlyList<ServiceRequestProblem>)Array.Empty<ServiceRequestProblem>();
            }
            catch
            {
                // ignore deserialization failures
            }
        }

        return Array.Empty<ServiceRequestProblem>();
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

    private string BuildLoginRedirectUrl()
    {
        var returnUrl = $"{Request.PathBase}{Request.Path}{Request.QueryString}";
        return $"/auth/login?ReturnUrl={Uri.EscapeDataString(returnUrl)}";
    }
}
