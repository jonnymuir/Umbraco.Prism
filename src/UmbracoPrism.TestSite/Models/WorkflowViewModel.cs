using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoPrism.Core.Models.Workflow;

namespace UmbracoPrism.TestSite.Models;

/// <summary>
/// View model for the WorkflowPage route-hijacking controller.
/// Extends <see cref="PrismWorkflowViewModel"/> with TestSite-specific properties (none currently).
/// </summary>
public class WorkflowViewModel : PrismWorkflowViewModel
{
    public WorkflowViewModel(IPublishedContent content, IPublishedValueFallback publishedValueFallback)
        : base(content, publishedValueFallback) { }

    // Add TestSite-specific properties here if needed
}
