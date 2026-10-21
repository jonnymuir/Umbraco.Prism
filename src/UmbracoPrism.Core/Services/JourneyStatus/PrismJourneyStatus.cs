namespace UmbracoPrism.Core.Services.JourneyStatus;

/// <summary>Whether a journey is waiting on something now, or that wait is over.</summary>
public enum PrismJourneyStatusKind
{
    /// <summary>Starts the journey's Live Activity or Live Update, or updates it if one is running.</summary>
    Waiting,

    /// <summary>Ends it, with this status showing for a short while.</summary>
    Done
}

/// <summary>
/// Where one of a member's journeys has got to, as shown on their phone's lock screen. Written by
/// the site, never taken from what the member entered, and saying what kind of thing happened rather
/// than an outcome or personal detail: "A decision has been made", not "Your licence was refused".
/// </summary>
/// <param name="Key">One per journey, such as the service request's ID.</param>
/// <param name="ServiceName">The service, such as "Apply for a juggling licence".</param>
/// <param name="Status">One short line, such as "Deciding your application".</param>
/// <param name="Step">The step reached, shown as "Step 2 of 3" when <paramref name="Steps"/> is given too.</param>
/// <param name="Steps">How many steps there are.</param>
/// <param name="Path">The site page tapping the status opens, such as "/apply-for-a-juggling-licence".</param>
/// <param name="Kind">Waiting or Done.</param>
public sealed record PrismJourneyStatus(
    string Key,
    string ServiceName,
    string Status,
    int? Step,
    int? Steps,
    string Path,
    PrismJourneyStatusKind Kind)
{
    public const int MaxKeyLength = 255;
    public const int MaxTextLength = 120;

    /// <summary>Why this status cannot be shown, or null when it can.</summary>
    public string? Problem() => this switch
    {
        _ when string.IsNullOrWhiteSpace(Key) || Key.Length > MaxKeyLength => $"Key must be 1 to {MaxKeyLength} characters.",
        _ when string.IsNullOrWhiteSpace(ServiceName) || ServiceName.Length > MaxTextLength => $"ServiceName must be 1 to {MaxTextLength} characters.",
        _ when string.IsNullOrWhiteSpace(Status) || Status.Length > MaxTextLength => $"Status must be 1 to {MaxTextLength} characters.",
        _ when Steps is < 1 || Step is < 0 || Step > Steps => "Step must be between 0 and Steps.",
        _ when !PrismDeepLinkPath.IsSitePath(Path) => "Path must be a path on this site, such as /apply.",
        _ => null
    };
}

/// <summary>Shows members where their journeys have got to, outside the app (Design/mobile-journey-status.md).</summary>
public interface IPrismJourneyStatusService
{
    /// <summary>
    /// Queues a status for the member's devices and returns at once. A background sender delivers
    /// it, so a slow or failed delivery never holds up whatever caused the change.
    /// </summary>
    /// <param name="userId">The member, as push notifications identify them: their email address.</param>
    /// <param name="tenantId">The tenant's host name.</param>
    /// <param name="status">Where the journey has got to.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="status"/> has a <see cref="PrismJourneyStatus.Problem"/>.</exception>
    void Publish(string userId, string tenantId, PrismJourneyStatus status);
}
