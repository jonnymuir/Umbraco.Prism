using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using System.Text.Json;
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
/// GET  — resolves or creates a workflow instance, renders the current step via Razor partials.
/// POST — advances the workflow state (PRG pattern).  Both verbs land on Index() because
///        Umbraco's content router always targets the Index action; the verb is inspected
///        manually so we avoid a Surface Controller.
/// </summary>
public class WorkflowPageController(
    ILogger<WorkflowPageController> logger,
    ICompositeViewEngine compositeViewEngine,
    IUmbracoContextAccessor umbracoContextAccessor,
    IPrismContext prismContext,
    IWorkflowInstanceService workflowInstanceService,
    IAntiforgery antiforgery)
    : RenderController(logger, compositeViewEngine, umbracoContextAccessor)
{
    private const string AnonUserCookie = "PrismAnonUserId";

    public override IActionResult Index()
    {
        if (HttpContext.Request.Method == HttpMethods.Post)
            return HandlePost().GetAwaiter().GetResult();

        return HandleGet();
    }

    // -----------------------------------------------------------------------
    // GET
    // -----------------------------------------------------------------------

    private IActionResult HandleGet()
    {
        var workflowKey = CurrentPage!.Value<string>("workflowKey") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(workflowKey))
        {
            return CurrentTemplate(ErrorViewModel(workflowKey,
                "No workflow key configured on this page. Set the 'workflowKey' property in the backoffice."));
        }

        var tenantId = prismContext.CurrentTenant?.Id.ToString() ?? "default";
        var userId = GetOrCreateAnonUserId();
        var cookieName = InstanceCookieName(workflowKey);

        // Restore problems from TempData (set by a failed POST)
        var problems = PopProblemsFromTempData();

        // Try to resume an existing instance
        WorkflowResponseEnvelope envelope;
        var existingInstanceId = HttpContext.Request.Cookies[cookieName];

        if (!string.IsNullOrEmpty(existingInstanceId))
        {
            envelope = workflowInstanceService
                .GetCurrentStateAsync(tenantId, userId, existingInstanceId)
                .GetAwaiter().GetResult();

            // Instance gone or error — start fresh
            if (envelope.ResponseState == "error")
            {
                envelope = CreateFreshInstance(tenantId, userId, workflowKey, cookieName);
            }
        }
        else
        {
            envelope = CreateFreshInstance(tenantId, userId, workflowKey, cookieName);
        }

        if (envelope.ResponseState == "error")
        {
            var msg = envelope.Problems.FirstOrDefault()?.Message
                ?? $"Could not start workflow '{workflowKey}'. Is the definition seeded?";
            return CurrentTemplate(ErrorViewModel(workflowKey, msg));
        }

        var vm = BuildViewModel(envelope, workflowKey, problems);
        return CurrentTemplate(vm);
    }

    private WorkflowResponseEnvelope CreateFreshInstance(
        string tenantId, string userId, string workflowKey, string cookieName)
    {
        var envelope = workflowInstanceService
            .CreateAsync(tenantId, userId, workflowKey)
            .GetAwaiter().GetResult();

        if (envelope.ResponseState != "error" && !string.IsNullOrEmpty(envelope.InstanceId))
        {
            HttpContext.Response.Cookies.Append(cookieName, envelope.InstanceId, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = HttpContext.Request.IsHttps
            });
        }

        return envelope;
    }

    // -----------------------------------------------------------------------
    // POST
    // -----------------------------------------------------------------------

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

        var tenantId = prismContext.CurrentTenant?.Id.ToString() ?? "default";
        var userId = GetOrCreateAnonUserId();

        var envelope = await workflowInstanceService.AdvanceAsync(
            tenantId, userId, instanceId, action, stateVersion, fieldValues);

        // On validation failure push problems into TempData so the GET can display them
        if (envelope.ResponseState == "error" && envelope.Problems.Count > 0)
        {
            TempData["WorkflowProblems"] = JsonSerializer.Serialize(envelope.Problems);
        }

        // On completion, clear the instance cookie so a fresh visit starts a new workflow
        if (envelope.ResponseState == "complete")
        {
            HttpContext.Response.Cookies.Delete(InstanceCookieName(workflowKey));
        }

        return Redirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private string GetOrCreateAnonUserId()
    {
        if (HttpContext.Request.Cookies.TryGetValue(AnonUserCookie, out var existing)
            && !string.IsNullOrEmpty(existing))
        {
            return existing;
        }

        var newId = $"anon-{Guid.NewGuid():N}";
        HttpContext.Response.Cookies.Append(AnonUserCookie, newId, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = HttpContext.Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });
        return newId;
    }

    private static string InstanceCookieName(string workflowKey)
        => $"PrismWorkflowInstance-{workflowKey}";

    private IReadOnlyList<WorkflowProblem> PopProblemsFromTempData()
    {
        if (TempData.TryGetValue("WorkflowProblems", out var raw) && raw is string json)
        {
            try
            {
                return (IReadOnlyList<WorkflowProblem>?)JsonSerializer.Deserialize<List<WorkflowProblem>>(json)
                    ?? Array.Empty<WorkflowProblem>();
            }
            catch
            {
                // ignore deserialization failures
            }
        }

        return Array.Empty<WorkflowProblem>();
    }

    private WorkflowViewModel BuildViewModel(
        WorkflowResponseEnvelope envelope,
        string workflowKey,
        IReadOnlyList<WorkflowProblem>? problems = null)
    {
        var render = envelope.Render;
        return new WorkflowViewModel
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

    private WorkflowViewModel ErrorViewModel(string workflowKey, string message) =>
        new()
        {
            WorkflowKey = workflowKey,
            HasError = true,
            ErrorMessage = message,
            ReturnUrl = HttpContext.Request.PathBase + HttpContext.Request.Path
        };
}
