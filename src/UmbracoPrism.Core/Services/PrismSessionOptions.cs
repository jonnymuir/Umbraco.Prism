using UmbracoPrism.Core.Sessions;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Lifetime and revalidation of the member session. Bind from <c>Prism:Session</c>.
/// </summary>
public class PrismSessionOptions
{
    /// <summary>Configuration section path used to bind these options.</summary>
    public const string SectionName = "Prism:Session";

    /// <summary>How long a session lasts without activity. Each request inside the window extends it. Default 8 hours.</summary>
    public int SlidingExpirationMinutes { get; set; } = 480;

    /// <summary>The longest a session lasts however active it is. Default 7 days.</summary>
    public int AbsoluteMaxLifetimeMinutes { get; set; } = 7 * 24 * 60;

    /// <summary>
    /// How often an active session is re-confirmed with the identity provider using its refresh
    /// token. A member removed or disabled at the provider loses the session within this interval.
    /// 0 turns revalidation off. Default 15 minutes.
    /// </summary>
    public int RevalidationIntervalMinutes { get; set; } = 15;

    /// <summary>
    /// Where sessions are kept on the server. <see cref="PrismSessionStorageKind.Database"/> (the
    /// default) needs no setup; <see cref="PrismSessionStorageKind.DistributedCache"/> uses the
    /// host's registered <c>IDistributedCache</c>, such as Redis.
    /// </summary>
    public PrismSessionStorageKind Storage { get; set; } = PrismSessionStorageKind.Database;
}
