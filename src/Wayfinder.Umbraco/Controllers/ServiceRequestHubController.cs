using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Extensions;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using Wayfinder.Umbraco.Models;
using Wayfinder.Umbraco.Services;
using Wayfinder.Models.ServiceDesign;

namespace Wayfinder.Umbraco.Controllers;

/// <summary>
/// Umbraco route-hijacking controller for the <c>serviceRequestHub</c> document type — a single "My
/// Workflows" surface across both workflow implementations a host may have running: the
/// business-app one (<see cref="IBusinessAppProcessManagerClient"/>'s default, unkeyed registration,
/// talking to a remote business app) and an optional keyed <c>"cms"</c> registration (e.g. Prism CMS
/// Workflow, in-process). Displays all workflow instances for the authenticated member from
/// both, merged into one list — a member shouldn't need to know or care which implementation
/// authored a given journey. The keyed client is genuinely optional: a host that hasn't
/// registered one under key <c>"cms"</c> at all just sees the unkeyed client's instances.
/// </summary>
public class ServiceRequestHubController : RenderController
{
    private readonly IBusinessAppProcessManagerClient _processManagerClient;
    private readonly IBusinessAppProcessManagerClient? _cmsProcessManagerClient;
    private readonly IPublishedValueFallback _publishedValueFallback;
    private readonly IPublishedContentQuery _publishedContentQuery;
    private readonly ILogger<ServiceRequestHubController> _logger;

    public ServiceRequestHubController(
        ILogger<ServiceRequestHubController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        IBusinessAppProcessManagerClient workflowClient,
        IServiceProvider serviceProvider,
        IPublishedValueFallback publishedValueFallback,
        IPublishedContentQuery publishedContentQuery)
        : base(logger, compositeViewEngine, umbracoContextAccessor)
    {
        _logger = logger;
        _processManagerClient = workflowClient;
        _cmsProcessManagerClient = serviceProvider.GetKeyedService<IBusinessAppProcessManagerClient>("cms");
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
        var businessAppEnvelope = await _processManagerClient.GetInstancesAsync();
        var cmsInstances = _cmsProcessManagerClient is null
            ? []
            : (await _cmsProcessManagerClient.GetInstancesAsync()).Instances;
        var allInstances = businessAppEnvelope.Instances
            .Concat(cmsInstances)
            .OrderByDescending(i => i.LastUpdatedAt)
            .ToList();

        var activeInstances = allInstances
            .Where(i => !i.IsCompleted)
            .Select(i => new ServiceRequestViewModel
            {
                Summary = i,
                ResumeUrl = ResolveStagePageUrl(i)
            })
            .ToList();

        var completedInstances = allInstances
            .Where(i => i.IsCompleted)
            .Select(i => new ServiceRequestViewModel
            {
                Summary = i,
                ResumeUrl = ResolveStagePageUrl(i)
            })
            .ToList();

        var vm = new ServiceRequestHubViewModel(CurrentPage!, _publishedValueFallback)
        {
            ActiveInstances = activeInstances,
            CompletedInstances = completedInstances
        };

        return CurrentTemplate(vm);
    }

    private string ResolveStagePageUrl(ServiceRequestSummary summary)
    {
        if (!string.IsNullOrWhiteSpace(summary.ServiceRequestPageUrl) && Url.IsLocalUrl(summary.ServiceRequestPageUrl))
        {
            // Append instanceId for non-completed instances
            if (!summary.IsCompleted && !string.IsNullOrWhiteSpace(summary.InstanceId))
            {
                var separator = summary.ServiceRequestPageUrl.Contains('?') ? "&" : "?";
                return $"{summary.ServiceRequestPageUrl}{separator}instanceId={Uri.EscapeDataString(summary.InstanceId)}";
            }
            return summary.ServiceRequestPageUrl;
        }

        if (string.IsNullOrWhiteSpace(summary.BlueprintKey))
            return CurrentPage?.Url() ?? "/";

        var stagePage = _publishedContentQuery
            .ContentAtRoot()
            .SelectMany(root => root.DescendantsOrSelf())
            .FirstOrDefault(content =>
                (content.ContentType.Alias == "stagePage" || content.ContentType.Alias == "cmsServiceRequestPage")
                && string.Equals(content.Value<string>("blueprintKey"), summary.BlueprintKey, StringComparison.OrdinalIgnoreCase));

        if (stagePage != null)
        {
            var baseUrl = stagePage.Url();
            // Append instanceId for non-completed instances
            if (!summary.IsCompleted && !string.IsNullOrWhiteSpace(summary.InstanceId))
            {
                return $"{baseUrl}?instanceId={Uri.EscapeDataString(summary.InstanceId)}";
            }
            return baseUrl;
        }

        _logger.LogWarning(
            "Workflow hub could not resolve a content-driven URL for workflow key {BlueprintKey}; defaulting to the hub page",
            summary.BlueprintKey);

        return CurrentPage?.Url() ?? "/";
    }

    private string BuildLoginRedirectUrl()
    {
        var returnUrl = $"{Request.PathBase}{Request.Path}{Request.QueryString}";
        return $"/auth/login?ReturnUrl={Uri.EscapeDataString(returnUrl)}";
    }
}
