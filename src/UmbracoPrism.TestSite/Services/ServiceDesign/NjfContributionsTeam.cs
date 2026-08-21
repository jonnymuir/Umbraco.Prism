using Wayfinder.Models.ServiceDesign;

namespace UmbracoPrism.TestSite.Services.ServiceDesign;

/// <summary>
/// The caseworker-side counterpart to <see cref="PublicVisitorQueue"/> — only the account(s) on
/// <see cref="MemberEmails"/> are treated as NJF Contributions Team members for this demo; a
/// signed-in Prism member who isn't on that roster gets <see cref="NoAccessProfile"/>, not this
/// one. Deliberately excludes <see cref="PublicVisitorQueue.Key"/> from every list below — the
/// citizen self-service queue and this team queue never share an <see cref="ActorProfile"/>,
/// because <see cref="ActorProfile.RestrictToInstanceOwner"/> is a single flag for the whole
/// profile: a caseworker profile needs it <see langword="false"/> (to see the whole team's queue),
/// which would otherwise let it see every other citizen's own instance on the owner-restricted
/// queue too.
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

    /// <summary>
    /// The reference site's own small, explicit team roster — mirrors the same
    /// email-keyed-membership-directory pattern <c>UmbracoPrism.MockBusinessApp</c>'s own
    /// <c>PrismBusinessApp:Members</c> config already uses for <c>/api/backoffice/me</c>'s
    /// <c>AssignedRole</c>, rather than inventing a second convention. Deliberately NOT "every
    /// authenticated Prism member" — <c>demo@prism.local</c> is the plain-member persona;
    /// <c>njf-caseworker@prism.local</c> (seeded in <c>keycloak/realm-export.json</c>) is the only
    /// account on this roster. A real host would resolve team membership from its own
    /// claims/role/group source (e.g. a Keycloak realm role) instead of a literal email list —
    /// this demo keeps it a plain C# set so the distinction is visible without also having to
    /// reason about OIDC claim-mapping configuration to understand the reference site.
    /// </summary>
    private static readonly HashSet<string> MemberEmails = new(StringComparer.OrdinalIgnoreCase)
    {
        "njf-caseworker@prism.local"
    };

    public static bool IsMember(string? email) =>
        !string.IsNullOrWhiteSpace(email) && MemberEmails.Contains(email);

    public static readonly ActorProfile AccessProfile = new()
    {
        VisibleQueues = [UploadKey, ReviewKey],
        StartableQueues = [UploadKey, ReviewKey],
        ActionableQueues = [UploadKey, ReviewKey],
        RestrictToInstanceOwner = false,
        Capabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { RoleGate },
        TeamIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { TeamId }
    };

    /// <summary>
    /// What an authenticated Prism member who is NOT on the team roster resolves to when they
    /// reach an NJF-only page/route directly. On the stage block (<c>/submit-contributions-file</c>)
    /// this produces a real "Access denied to start this queue" envelope; on the worklist block
    /// (<c>/caseworker-queue</c>) it just filters every row out, rendering the same empty-state
    /// text a genuinely-empty team queue would — no security leak either way (nothing is visible
    /// or actionable), just a less explicit refusal on the worklist side. Deliberately NOT an
    /// empty <see cref="ActorProfile"/> — <c>ActorProfile.CanViewQueue</c>/<c>CanStartQueue</c>/
    /// <c>CanActInQueue</c> treat an *empty* allow-list as fully unrestricted (matching
    /// <see cref="ActorProfile.UnrestrictedOwner"/>'s own shape), the opposite of "deny
    /// everything" — a sentinel queue key that can never match a real queue is what actually
    /// denies every queue.
    /// </summary>
    public static readonly ActorProfile NoAccessProfile = new()
    {
        VisibleQueues = ["__no-access__"],
        StartableQueues = ["__no-access__"],
        ActionableQueues = ["__no-access__"]
    };
}
