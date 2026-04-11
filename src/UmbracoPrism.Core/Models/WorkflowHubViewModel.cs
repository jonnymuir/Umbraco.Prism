using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.Core.Models;

public class WorkflowHubViewModel : PublishedContentWrapped
{
    public IReadOnlyList<WorkflowInstanceViewModel> ActiveInstances { get; init; } = Array.Empty<WorkflowInstanceViewModel>();
    public IReadOnlyList<WorkflowInstanceViewModel> CompletedInstances { get; init; } = Array.Empty<WorkflowInstanceViewModel>();

    public WorkflowHubViewModel(IPublishedContent content, IPublishedValueFallback publishedValueFallback)
        : base(content, publishedValueFallback) { }
}

public class WorkflowInstanceViewModel
{
    public WorkflowInstanceSummary Summary { get; init; } = null!;
    public string ResumeUrl { get; init; } = "#";
}
