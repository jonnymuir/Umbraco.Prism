using UmbracoPrism.Shared.Models.ServiceDesign;

namespace UmbracoPrism.Core.Services.ServiceDesign;

/// <summary>
/// The single well-known queue every CMS Workflow definition runs on — declared once here and
/// threaded through the backoffice editor host (as the only entry in <c>availableQueues</c>,
/// which is what naturally locks the editor's queue-picker to single-queue authoring), the
/// runtime host's <see cref="UmbracoPrism.Shared.Models.ServiceDesign.ActorProfile"/>
/// construction, and every CMS Workflow seed definition's own <c>queues</c> array. No component
/// infers "this is CMS mode" from queue-count — they all read this shared constant instead.
/// </summary>
public static class CmsQueue
{
    public const string Key = "cms-visitor";
    public const string DisplayName = "Site visitor";

    /// <summary>
    /// The access profile every CMS Workflow entry point (the page controller's client, a file
    /// download endpoint, anything else that resolves a visitor's own instance) constructs
    /// identically — a visitor can only see/start/act on this one queue, and only their own
    /// instance within it.
    /// </summary>
    public static readonly ActorProfile AccessProfile = new()
    {
        VisibleQueues = [Key],
        StartableQueues = [Key],
        ActionableQueues = [Key],
        RestrictToInstanceOwner = true
    };
}
