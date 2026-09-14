using Wayfinder.Models.ServiceDesign;

namespace UmbracoPrism.TestSite.Services.ServiceDesign;

/// <summary>
/// The member-side counterpart to <see cref="PublicVisitorQueue"/> and
/// <see cref="NjfContributionsTeam"/> — a third, distinct persona: any signed-in Prism member
/// (not role-gated to a named roster like <see cref="NjfContributionsTeam"/>, and not anonymous
/// like <see cref="PublicVisitorQueue"/>), modelling their own savings pot scenarios. Money
/// Modeller's own <c>queues</c> array (<c>money-modeller.json</c>, copied verbatim from
/// <c>UmbracoPrism.MockBusinessApp/service-blueprints</c> rather than renamed to fit TestSite's
/// other queue-key conventions, so the two copies stay easy to diff/re-sync) uses <c>web-user</c>
/// for the citizen-facing modelling stages and <c>business-user</c> for the formal-quote review
/// stages; only <see cref="MemberQueueKey"/> is wired up here since nothing seeds a worklist page
/// for the reviewer side (see <c>WayfinderServicePageSeeder</c>'s own remarks).
/// </summary>
public static class MoneyModellerAccess
{
    public const string MemberQueueKey = "web-user";

    /// <summary>
    /// <see cref="ActorProfile.RestrictToInstanceOwner"/> is true here (unlike
    /// <see cref="NjfContributionsTeam.AccessProfile"/>) — this is one member modelling their own
    /// scenarios, not a shared team worklist, so nobody should see another member's instance.
    /// </summary>
    public static readonly ActorProfile AccessProfile = new()
    {
        VisibleQueues = [MemberQueueKey],
        StartableQueues = [MemberQueueKey],
        ActionableQueues = [MemberQueueKey],
        RestrictToInstanceOwner = true
    };
}
