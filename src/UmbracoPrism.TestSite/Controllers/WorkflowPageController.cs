using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using UmbracoPrism.Core.Models;
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
    IPrismContext prismContext,
    IBusinessAppWorkflowClient workflowClient,
    IPublishedValueFallback publishedValueFallback,
    IAntiforgery antiforgery)
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

        return HandleGet();
    }

    // -----------------------------------------------------------------------
    // GET — ask the Business App what to show
    // -----------------------------------------------------------------------

    /// <summary>
    /// Handles GET requests: calls the Business App to get the current workflow state and renders it.
    /// </summary>
    /// <returns>A view model for rendering the current workflow state.</returns>
    private IActionResult HandleGet()
    {
        var workflowKey = CurrentPage!.Value<string>("workflowKey") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(workflowKey))
        {
            return CurrentTemplate(ErrorViewModel(workflowKey,
                "No workflow key configured on this page. Set the 'workflowKey' property in the backoffice."));
        }

        var tenantId = prismContext.CurrentTenant?.Id.ToString() ?? "default";
        var userId = GetMemberUserId();
        var problems = PopProblemsFromTempData();

        var envelope = workflowClient
            .GetCurrentAsync(workflowKey, tenantId, userId)
            .GetAwaiter().GetResult();

        if (envelope.ResponseState == "error")
        {
            var msg = envelope.Problems.FirstOrDefault()?.Message
                ?? $"Could not start workflow '{workflowKey}'. Is the Business App running?";
            return CurrentTemplate(ErrorViewModel(workflowKey, msg));
        }

        return CurrentTemplate(BuildViewModel(envelope, workflowKey, problems));
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
    /// Field values are extracted from form keys prefixed with "fields[" (e.g., "fields[retirement-age]").
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
        var instanceId = form["InstanceId"].ToString();
        var workflowKey = form["WorkflowKey"].ToString();
        var returnUrl = form["ReturnUrl"].ToString();
        var action = form["Action"].ToString();
        var stateVersion = int.TryParse(form["StateVersion"], out var sv) ? sv : 0;

        // Collect submitted field values (form keys prefixed "fields[…]")
        var fieldValues = form.Keys
            .Where(k => k.StartsWith("fields[", StringComparison.Ordinal) && k.EndsWith("]"))
            .ToDictionary(
                k => k[7..^1],
                k => (object?)form[k].ToString());

        if (string.IsNullOrEmpty(instanceId) || string.IsNullOrEmpty(action))
        {
            logger.LogWarning("Workflow POST: missing InstanceId or Action");
            return Redirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
        }

        // SECURITY: Always derive tenant/user from current session, never trust form data
        var tenantId = prismContext.CurrentTenant?.Id.ToString() ?? "default";
        var userId = GetMemberUserId();
        
        // SECURITY: Validate that form-submitted tenant/user match session identity
        var formTenantId = form["TenantId"].ToString();
        var formUserId = form["UserId"].ToString();
        
        if (!string.IsNullOrEmpty(formTenantId) && formTenantId != tenantId)
        {
            logger.LogWarning("SECURITY: Workflow POST tenant mismatch. Session={Session}, Form={Form}", tenantId, formTenantId);
            return Forbid();
        }
        
        if (!string.IsNullOrEmpty(formUserId) && formUserId != userId)
        {
            logger.LogWarning("SECURITY: Workflow POST user mismatch. Session={Session}, Form={Form}", userId, formUserId);
            return Forbid();
        }

        var envelope = await workflowClient.AdvanceAsync(
            workflowKey, tenantId, userId, instanceId, action, stateVersion, fieldValues);

        if (envelope.ResponseState == "error" && envelope.Problems.Count > 0)
            TempData["WorkflowProblems"] = JsonSerializer.Serialize(envelope.Problems);

        return Redirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>Returns the authenticated member's OID claim as the stable user identifier.</summary>
    private string GetMemberUserId() =>
        User.FindFirst("oid")?.Value
        ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? string.Empty;

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
