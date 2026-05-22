using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Umbraco.Extensions;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;
using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Umbraco route-hijacking controller for the <c>workflowHub</c> document type.
/// Displays all workflow instances for the authenticated member.
/// </summary>
public class WorkflowHubController : RenderController
{
    private readonly IBusinessAppWorkflowClient _workflowClient;
    private readonly IPublishedValueFallback _publishedValueFallback;
    private readonly IPublishedContentQuery _publishedContentQuery;
    private readonly ILogger<WorkflowHubController> _logger;

    public WorkflowHubController(
        ILogger<WorkflowHubController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        IBusinessAppWorkflowClient workflowClient,
        IPublishedValueFallback publishedValueFallback,
        IPublishedContentQuery publishedContentQuery)
        : base(logger, compositeViewEngine, umbracoContextAccessor)
    {
        _logger = logger;
        _workflowClient = workflowClient;
        _publishedValueFallback = publishedValueFallback;
        _publishedContentQuery = publishedContentQuery;
    }

    public override IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Redirect(BuildLoginRedirectUrl());
        }

        return IndexAsync().GetAwaiter().GetResult();
    }

    private async Task<IActionResult> IndexAsync()
    {
        var envelope = await _workflowClient.GetInstancesAsync();

        var activeInstances = envelope.Instances
            .Where(i => !i.IsCompleted)
            .Select(i => new WorkflowInstanceViewModel
            {
                Summary = i,
                ResumeUrl = ResolveWorkflowPageUrl(i)
            })
            .ToList();

        var completedInstances = envelope.Instances
            .Where(i => i.IsCompleted)
            .Select(i => new WorkflowInstanceViewModel
            {
                Summary = i,
                ResumeUrl = ResolveWorkflowPageUrl(i)
            })
            .ToList();

        var vm = new WorkflowHubViewModel(CurrentPage!, _publishedValueFallback)
        {
            ActiveInstances = activeInstances,
            CompletedInstances = completedInstances
        };

        return CurrentTemplate(vm);
    }

    private string ResolveWorkflowPageUrl(WorkflowInstanceSummary summary)
    {
        if (!string.IsNullOrWhiteSpace(summary.WorkflowPageUrl) && Url.IsLocalUrl(summary.WorkflowPageUrl))
        {
            // Append instanceId for non-completed instances
            if (!summary.IsCompleted && !string.IsNullOrWhiteSpace(summary.InstanceId))
            {
                var separator = summary.WorkflowPageUrl.Contains('?') ? "&" : "?";
                return $"{summary.WorkflowPageUrl}{separator}instanceId={Uri.EscapeDataString(summary.InstanceId)}";
            }
            return summary.WorkflowPageUrl;
        }

        if (string.IsNullOrWhiteSpace(summary.WorkflowKey))
            return CurrentPage?.Url() ?? "/";

        var workflowPage = _publishedContentQuery
            .ContentAtRoot()
            .SelectMany(root => root.DescendantsOrSelf())
            .FirstOrDefault(content =>
                content.ContentType.Alias == "workflowPage"
                && string.Equals(content.Value<string>("workflowKey"), summary.WorkflowKey, StringComparison.OrdinalIgnoreCase));

        if (workflowPage != null)
        {
            var baseUrl = workflowPage.Url();
            // Append instanceId for non-completed instances
            if (!summary.IsCompleted && !string.IsNullOrWhiteSpace(summary.InstanceId))
            {
                return $"{baseUrl}?instanceId={Uri.EscapeDataString(summary.InstanceId)}";
            }
            return baseUrl;
        }

        _logger.LogWarning(
            "Workflow hub could not resolve a content-driven URL for workflow key {WorkflowKey}; defaulting to the hub page",
            summary.WorkflowKey);

        return CurrentPage?.Url() ?? "/";
    }

    private string BuildLoginRedirectUrl()
    {
        var returnUrl = $"{Request.PathBase}{Request.Path}{Request.QueryString}";
        return $"/auth/login?ReturnUrl={Uri.EscapeDataString(returnUrl)}";
    }
}
