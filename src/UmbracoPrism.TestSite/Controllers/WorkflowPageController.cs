using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.Core.Services;
using UmbracoPrism.TestSite.Models;

namespace UmbracoPrism.TestSite.Controllers;

/// <summary>
/// Umbraco route-hijacking controller for the <c>workflowPage</c> document type.
/// Naming convention: <c>WorkflowPageController</c> → alias <c>workflowPage</c>.
///
/// GET  — calls the Business App to ask "what should this user do next?" and renders the result.
/// POST — submits the member's data to the Business App and redirects (PRG pattern).
///
/// Both verbs land on Index() because Umbraco's content router always targets the Index action;
/// the verb is inspected manually so we avoid a Surface Controller.
/// </summary>
/// <remarks>
/// Requires an authenticated PrismMemberCookie session; unauthenticated requests are
/// challenged by the framework. The authenticated member's OID claim is used as the
/// stable user identifier passed to the Business App.
///
/// Antiforgery validation is performed manually rather than via an attribute because this
/// method serves both GET and POST and the attribute cannot be applied to such methods.
/// </remarks>
[Authorize(AuthenticationSchemes = "PrismMemberCookie")]
public class WorkflowPageController(
    ILogger<WorkflowPageController> logger,
    ICompositeViewEngine compositeViewEngine,
    IUmbracoContextAccessor umbracoContextAccessor,
    IBusinessAppWorkflowClient workflowClient,
    IPublishedValueFallback publishedValueFallback,
    IAntiforgery antiforgery,
    IWorkflowStepNonceService nonceService,
    IWorkflowFieldValidator fieldValidator)
    : RenderController(logger, compositeViewEngine, umbracoContextAccessor)
{
    /// <summary>
    /// Routes GET and POST requests for the workflow page.
    /// Redirects to login if the member is not authenticated.
    /// </summary>
    /// <returns>
    /// For GET: A rendered workflow view with current state and fields to collect.
    /// For POST: A redirect to the current page or a configured return URL (PRG pattern).
    /// For unauthenticated requests: A redirect to the login page.
    /// </returns>
    public override IActionResult Index()
    {
        if (HttpContext.Request.Method == HttpMethods.Post)
            return HandlePost().GetAwaiter().GetResult();

        return HandleGet().GetAwaiter().GetResult();
    }

    // -----------------------------------------------------------------------
    // GET — ask the Business App what to show
    // -----------------------------------------------------------------------

    /// <summary>
    /// Handles GET requests: calls the Business App to get the current workflow state and renders it.
    /// </summary>
    /// <returns>A view model for rendering the current workflow state.</returns>
    private async Task<IActionResult> HandleGet()
    {
        var workflowKey = CurrentPage!.Value<string>("workflowKey") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(workflowKey))
        {
            return CurrentTemplate(ErrorViewModel(workflowKey,
                "No workflow key configured on this page. Set the 'workflowKey' property in the backoffice."));
        }

        var problems = PopProblemsFromTempData();

        var envelope = workflowClient
            .GetCurrentAsync(workflowKey)
            .GetAwaiter().GetResult();

        if (envelope.ResponseState == "error")
        {
            var msg = envelope.Problems.FirstOrDefault()?.Message
                ?? $"Could not start workflow '{workflowKey}'. Is the Business App running?";
            return CurrentTemplate(ErrorViewModel(workflowKey, msg));
        }

        // Collect all fields from the render payload for nonce caching
        var allFields = envelope.Render?.FieldGroups
            .SelectMany(g => g.Fields)
            .ToList() ?? new List<FieldRenderPayload>();

        var nonce = await nonceService.CreateAsync(allFields);
        var vm = BuildViewModel(envelope, workflowKey, problems);
        vm.Nonce = nonce;
        return CurrentTemplate(vm);
    }

    // -----------------------------------------------------------------------
    // POST — submit data to the Business App and redirect (PRG)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Handles POST requests: validates the form, collects field values, submits to the Business App,
    /// and redirects to the return URL (Post-Redirect-Get pattern).
    /// </summary>
    /// <returns>A redirect response; on validation error, redirects to the current or specified return URL.</returns>
    /// <remarks>
    /// Performs antiforgery validation manually to handle the special case of a method serving both GET and POST.
    /// Field values are extracted from form keys prefixed with "fields[" (e.g., "fields[full-name]").
    /// Problems from the Business App (if any) are serialized to TempData for display on the next GET.
    /// </remarks>
    private async Task<IActionResult> HandlePost()
    {
        // Manual antiforgery check (replaces [ValidateAntiForgeryToken] attribute
        // which cannot be applied to a method that also serves GET via the same action)
        try
        {
            await antiforgery.ValidateRequestAsync(HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            logger.LogWarning("Workflow POST: antiforgery validation failed");
            return BadRequest("Invalid form submission.");
        }

        var form = HttpContext.Request.Form;

        // 1. Nonce validation — tamper-proofing
        var nonce = form["Nonce"].ToString();
        if (string.IsNullOrEmpty(nonce))
        {
            logger.LogWarning("Workflow POST: missing nonce — possible form tampering");
            return Redirect(string.IsNullOrEmpty(form["ReturnUrl"].ToString()) ? "/" : form["ReturnUrl"].ToString());
        }

        var authoritativeFields = await nonceService.ResolveAsync(nonce);
        if (authoritativeFields == null)
        {
            logger.LogWarning("Workflow POST: nonce expired or invalid — redirecting to GET");
            return Redirect(string.IsNullOrEmpty(form["ReturnUrl"].ToString()) ? "/" : form["ReturnUrl"].ToString());
        }

        // 2. Structural validation
        var submittedFields = form.Keys
            .Where(k => k.StartsWith("fields[", StringComparison.Ordinal) && k.EndsWith("]"))
            .ToDictionary(
                k => k[7..^1],
                k => form[k].ToString());

        var validationResult = fieldValidator.Validate(authoritativeFields, submittedFields);
        if (!validationResult.IsValid)
        {
            // Convert to WorkflowProblem list and store in TempData (PRG pattern)
            var problems = validationResult.Errors
                .Select(e => new WorkflowProblem { FieldKey = e.Key, Message = e.Value, Code = "validation_error" })
                .ToList();
            TempData["WorkflowProblems"] = JsonSerializer.Serialize(problems);
            return Redirect(string.IsNullOrEmpty(form["ReturnUrl"].ToString()) ? "/" : form["ReturnUrl"].ToString());
        }
        var instanceId = form["InstanceId"].ToString();
        var workflowKey = form["WorkflowKey"].ToString();
        var returnUrl = form["ReturnUrl"].ToString();
        var action = form["Action"].ToString();
        var stateVersion = int.TryParse(form["StateVersion"], out var sv) ? sv : 0;

        // Use already-validated submittedFields (converted to object? for AdvanceAsync)
        var fieldValues = submittedFields.ToDictionary(
            kvp => kvp.Key,
            kvp => (object?)kvp.Value);

        if (string.IsNullOrEmpty(instanceId) || string.IsNullOrEmpty(action))
        {
            logger.LogWarning("Workflow POST: missing InstanceId or Action");
            return Redirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
        }

        var envelope = await workflowClient.AdvanceAsync(
            workflowKey, instanceId, action, stateVersion, fieldValues);

        if (envelope.ResponseState == "error" && envelope.Problems.Count > 0)
            TempData["WorkflowProblems"] = JsonSerializer.Serialize(envelope.Problems);

        return Redirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Extracts problems from TempData and deserializes them into WorkflowProblem objects.
    /// Used to display validation errors or business logic problems from the previous Business App call.
    /// </summary>
    /// <returns>A list of problems; empty if none are present or deserialization fails.</returns>
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

    /// <summary>
    /// Builds the WorkflowViewModel from a Business App response envelope.
    /// </summary>
    /// <param name="envelope">The WorkflowResponseEnvelope from the Business App.</param>
    /// <param name="workflowKey">The workflow definition key configured on the page.</param>
    /// <param name="problems">Optional validation problems to display to the user.</param>
    /// <returns>A WorkflowViewModel ready for view rendering.</returns>
    private WorkflowViewModel BuildViewModel(
        WorkflowResponseEnvelope envelope,
        string workflowKey,
        IReadOnlyList<WorkflowProblem>? problems = null)
    {
        var render = envelope.Render;
        return new WorkflowViewModel(CurrentPage!, publishedValueFallback)
        {
            InstanceId = envelope.InstanceId,
            StateVersion = envelope.StateVersion,
            WorkflowKey = workflowKey,
            ReturnUrl = HttpContext.Request.PathBase + HttpContext.Request.Path,
            Archetype = render?.Archetype ?? string.Empty,
            StateDisplayName = render?.StateDisplayName ?? string.Empty,
            FieldGroups = render?.FieldGroups ?? Array.Empty<FieldGroupRenderPayload>(),
            AvailableActions = render?.AvailableActions ?? Array.Empty<WorkflowAction>(),
            Problems = problems ?? Array.Empty<WorkflowProblem>()
        };
    }

    /// <summary>
    /// Builds an error view model when the workflow cannot be started or an unexpected error occurs.
    /// </summary>
    /// <param name="workflowKey">The workflow key that failed.</param>
    /// <param name="message">The error message to display to the user.</param>
    /// <returns>A WorkflowViewModel in error state.</returns>
    private WorkflowViewModel ErrorViewModel(string workflowKey, string message) =>
        new(CurrentPage!, publishedValueFallback)
        {
            WorkflowKey = workflowKey,
            HasError = true,
            ErrorMessage = message,
            ReturnUrl = HttpContext.Request.PathBase + HttpContext.Request.Path
        };
}
