using Wayfinder.Models.ServiceDesign;

namespace UmbracoPrism.TestSite.Services.ServiceDesign;

/// <summary>
/// The caseworker-side counterpart to <see cref="PublicVisitorQueue"/> — any authenticated Prism
/// member is treated as an NJF Contributions Team member for this demo (a single fictional
/// persona is enough to show Prism + Wayfinder.Umbraco's worklist composing together; a real host
/// would resolve team membership from its own claims/role source instead). Deliberately excludes
/// <see cref="PublicVisitorQueue.Key"/> from every list below — the citizen self-service queue and
/// this team queue never share an <see cref="ActorProfile"/>, because
/// <see cref="ActorProfile.RestrictToInstanceOwner"/> is a single flag for the whole profile: a
/// caseworker profile needs it <see langword="false"/> (to see the whole team's queue), which
/// would otherwise let it see every other citizen's own instance on the owner-restricted queue too.
/// </summary>
/// <remarks>
/// Two queues, not one — confirmed live: pickup is mandatory for any non-owner-restricted queue
/// with no <c>assign-to-initiator</c> policy (see docs/guides/team-assignment.md), including the
/// very first stage of a brand-new instance. A single team-tray queue covering both "submit a new
/// file" and "review a decision" would leave the initiator unable to submit their own first form
/// (nothing exists yet to pick up). <see cref="UploadKey"/> is <c>assign-to-initiator</c> so
/// starting fresh needs no pickup; <see cref="ReviewKey"/> is <c>team-tray</c> so the worklist's
/// pickup/putback UI actually has something to demonstrate.
/// </remarks>
public static class NjfContributionsTeam
{
    public const string UploadKey = "njf-upload";
    public const string ReviewKey = "njf-review";
    public const string TeamId = "njf-contributions-team";
    public const string RoleGate = "njf-contributions-review";

    public static readonly ActorProfile AccessProfile = new()
    {
        VisibleQueues = [UploadKey, ReviewKey],
        StartableQueues = [UploadKey, ReviewKey],
        ActionableQueues = [UploadKey, ReviewKey],
        RestrictToInstanceOwner = false,
        Capabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { RoleGate },
        TeamIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { TeamId }
    };
}
