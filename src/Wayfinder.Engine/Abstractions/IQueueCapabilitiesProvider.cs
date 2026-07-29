namespace Wayfinder.Engine.Abstractions;

/// <summary>
/// Host-implemented extension point declaring which PrismComponent "type" discriminators
/// (see <c>PrismComponentTypeCatalog</c>) a queue's host application can actually render.
/// Optional at the toolkit level (see <see cref="Services.ServiceBlueprintAuthoringService"/>'s
/// nullable constructor param) — a host that doesn't care about this simply never registers
/// one, and the capability check is skipped entirely.
/// </summary>
public interface IQueueCapabilitiesProvider
{
    /// <summary>
    /// Declared component-type discriminators supported for <paramref name="queueKey"/>.
    /// Returns null when this queue has no explicit declaration at all — unrestricted, not
    /// this host's concern (e.g. a queue actually served by a different app). Returns a
    /// non-null, possibly empty list when the queue IS declared — an empty list is an honest
    /// "this host currently renders zero component types for this queue." Null vs. empty must
    /// stay distinguishable; this is a different convention to
    /// <c>ActorProfile</c>'s "empty list = unrestricted", deliberately so.
    /// </summary>
    IReadOnlyList<string>? GetSupportedComponentTypes(string queueKey);

    /// <summary>
    /// Every queue this host has an explicit declaration for, keyed by queue key. Backs
    /// discovering what's safe to author for a queue before drafting a stage for it.
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyList<string>> GetAllDeclaredCapabilities();
}

/// <summary>
/// Dictionary-backed reference implementation for hosts with a fixed, compile-time-known
/// capability set — the common case, since a host's rendering surface doesn't usually change
/// at runtime. Pass an <see cref="StringComparer.OrdinalIgnoreCase"/>-keyed dictionary,
/// matching <c>StageDefinition.QueueKey</c>/<c>ActorProfile</c>'s own
/// case-insensitivity convention.
/// </summary>
public sealed class StaticQueueCapabilitiesProvider(
    IReadOnlyDictionary<string, IReadOnlyList<string>> capabilitiesByQueueKey) : IQueueCapabilitiesProvider
{
    public IReadOnlyList<string>? GetSupportedComponentTypes(string queueKey) =>
        capabilitiesByQueueKey.TryGetValue(queueKey, out var types) ? types : null;

    public IReadOnlyDictionary<string, IReadOnlyList<string>> GetAllDeclaredCapabilities() =>
        capabilitiesByQueueKey;
}
