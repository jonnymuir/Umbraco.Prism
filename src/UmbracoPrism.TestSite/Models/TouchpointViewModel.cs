using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoPrism.Core.Models.ServiceDesign;

namespace UmbracoPrism.TestSite.Models;

/// <summary>
/// View model for the TouchpointPage route-hijacking controller.
/// Extends <see cref="PrismServiceRequestViewModel"/> with TestSite-specific properties (none currently).
/// </summary>
public class TouchpointViewModel : PrismServiceRequestViewModel
{
    public TouchpointViewModel(IPublishedContent content, IPublishedValueFallback publishedValueFallback)
        : base(content, publishedValueFallback) { }

    // Add TestSite-specific properties here if needed
}
