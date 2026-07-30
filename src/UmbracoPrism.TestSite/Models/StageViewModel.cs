using Umbraco.Cms.Core.Models.PublishedContent;
using Wayfinder.Umbraco.Models;

namespace UmbracoPrism.TestSite.Models;

/// <summary>
/// View model for the StagePage route-hijacking controller.
/// Extends <see cref="PrismServiceRequestViewModel"/> with TestSite-specific properties (none currently).
/// </summary>
public class StageViewModel : PrismServiceRequestViewModel
{
    public StageViewModel(IPublishedContent content, IPublishedValueFallback publishedValueFallback)
        : base(content, publishedValueFallback) { }

    // Add TestSite-specific properties here if needed
}
