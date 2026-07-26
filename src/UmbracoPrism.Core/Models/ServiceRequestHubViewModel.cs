using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoPrism.Shared.Models.ServiceDesign;

namespace UmbracoPrism.Core.Models;

public class ServiceRequestHubViewModel : PublishedContentWrapped
{
    public IReadOnlyList<ServiceRequestViewModel> ActiveInstances { get; init; } = Array.Empty<ServiceRequestViewModel>();
    public IReadOnlyList<ServiceRequestViewModel> CompletedInstances { get; init; } = Array.Empty<ServiceRequestViewModel>();

    public ServiceRequestHubViewModel(IPublishedContent content, IPublishedValueFallback publishedValueFallback)
        : base(content, publishedValueFallback) { }
}

public class ServiceRequestViewModel
{
    public ServiceRequestSummary Summary { get; init; } = null!;
    public string ResumeUrl { get; init; } = "#";
}
