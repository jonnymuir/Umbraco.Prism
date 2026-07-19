namespace UmbracoPrism.TestSite.Services;

/// <summary>
/// A visitor's Juggling Society membership record, as resolved by
/// <see cref="IJugglingSocietyMembershipClient"/>. <see cref="Tier"/> is the empty string for a
/// non-member (anonymous visitor, or a logged-in Prism Member who just isn't a Society member) —
/// deliberately never <see langword="null"/>, so the "apply-for-a-juggling-licence" definition's
/// <c>member.tier</c> calculation field is always resolvable regardless of who's asking. See
/// <c>CmsWorkflowEngine</c>'s <c>serviceInputsResolver</c> wiring in <c>TestSiteComposer</c>.
/// </summary>
public sealed record JugglingSocietyMembership(string Tier);

/// <summary>
/// Mock membership lookup demonstrating the extension point a real CMS Workflow implementation
/// uses to default form data from an external system of record for a logged-in member — the same
/// role <c>MemberRecordService</c> plays for Money Modeller's business-workflow demo, just a
/// simpler shape (one field, not six).
/// </summary>
public class JugglingSocietyMembershipClient : IJugglingSocietyMembershipClient
{
    private static readonly JugglingSocietyMembership NotAMember = new(Tier: "");

    private static readonly Dictionary<string, JugglingSocietyMembership> Members = new(StringComparer.OrdinalIgnoreCase)
    {
        ["demo@prism.local"] = new JugglingSocietyMembership(Tier: "Competitive")
    };

    public JugglingSocietyMembership GetForUser(string userId) =>
        Members.TryGetValue(userId, out var membership) ? membership : NotAMember;
}

public interface IJugglingSocietyMembershipClient
{
    JugglingSocietyMembership GetForUser(string userId);
}
