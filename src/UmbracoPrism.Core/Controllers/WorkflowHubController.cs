using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Umbraco route-hijacking controller for the <c>workflowHub</c> document type.
/// Displays all workflow instances for the authenticated member.
/// </summary>
public class WorkflowHubController : RenderController
{
    private readonly IBusinessAppWorkflowClient _workflowClient;
    private readonly IPublishedValueFallback _publishedValueFallback;

    public WorkflowHubController(
        ILogger<WorkflowHubController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        IBusinessAppWorkflowClient workflowClient,
        IPublishedValueFallback publishedValueFallback)
        : base(logger, compositeViewEngine, umbracoContextAccessor)
    {
        _workflowClient = workflowClient;
        _publishedValueFallback = publishedValueFallback;
    }

    public override IActionResult Index()
    {
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
                ResumeUrl = $"/{i.WorkflowKey}" // Simple resolution for MVP
            })
            .ToList();

        var completedInstances = envelope.Instances
            .Where(i => i.IsCompleted)
            .Select(i => new WorkflowInstanceViewModel
            {
                Summary = i,
                ResumeUrl = $"/{i.WorkflowKey}"
            })
            .ToList();

        var vm = new WorkflowHubViewModel(CurrentPage!, _publishedValueFallback)
        {
            ActiveInstances = activeInstances,
            CompletedInstances = completedInstances
        };

        return CurrentTemplate(vm);
    }
}
