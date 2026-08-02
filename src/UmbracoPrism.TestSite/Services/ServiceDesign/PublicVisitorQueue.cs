using Wayfinder.Models.ServiceDesign;

namespace UmbracoPrism.TestSite.Services.ServiceDesign;

/// <summary>
/// The single well-known queue TestSite's own anonymous-first, in-Umbraco-hosted service
/// blueprint demo runs on — declared once here and threaded through the runtime host's
/// <see cref="Wayfinder.Models.ServiceDesign.ActorProfile"/> construction and every seed
/// definition's own <c>queues</c> array. No component infers "this is the public demo" from
/// queue-count — they all read this shared constant instead.
/// </summary>
public static class PublicVisitorQueue
{
    public const string Key = "public-visitor";
    public const string DisplayName = "Site visitor touchpoints";

    /// <summary>
    /// The access profile every entry point (the page controller's client, a file download
    /// endpoint, anything else that resolves a visitor's own instance) constructs identically —
    /// a visitor can only see/start/act on this one queue, and only their own instance within it.
    /// </summary>
    public static readonly ActorProfile AccessProfile = new()
    {
        VisibleQueues = [Key],
        StartableQueues = [Key],
        ActionableQueues = [Key],
        RestrictToInstanceOwner = true
    };
}
