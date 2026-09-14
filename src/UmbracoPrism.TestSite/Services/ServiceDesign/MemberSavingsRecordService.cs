namespace UmbracoPrism.TestSite.Services.ServiceDesign;

/// <summary>
/// A member's savings record as held by the (mock) NJF records system — Money Modeller's own
/// service-sourced input. Monetary values are per-year (the Guaranteed Award) or absolute
/// (lump sum/Flexible Pot), in today's money. See money-modeller.json's own <c>member.*</c>
/// calculation fields for the exact shape this feeds.
/// </summary>
public sealed record MemberSavingsRecord
{
    public string Name { get; init; } = "";
    public bool Active { get; init; }
    public int Age { get; init; }
    public decimal Salary { get; init; }
    public decimal AccruedPension { get; init; }
    public decimal AccruedLump { get; init; }
    public decimal DcPot { get; init; }
}

/// <summary>
/// Mock member-record lookup demonstrating the same service-sourced-input extension point
/// <see cref="IJugglingSocietyMembershipClient"/> does for the juggling licence discount — a real
/// deployment would call its own member records system here. TestSite has one real signed-in
/// demo persona (<c>demo@prism.local</c>, see <c>NjfContributionsTeam</c>'s own remarks on why
/// that's deliberately not the NJF caseworker roster either), so unlike
/// <c>UmbracoPrism.MockBusinessApp.Services.MoneyModeller.MemberRecordService</c>'s own three
/// personas (a separate, unrelated demo surface — MockBusinessApp's business-service-blueprint
/// authoring toolkit, not reachable through TestSite), one stable record covers every signed-in
/// TestSite member: active, partway through an NJF membership, with both a Guaranteed Award
/// balance and a Flexible Pot, so Money Modeller's sliders/chart/stat-group all have something
/// real to model rather than rendering blank figures.
/// </summary>
public class MemberSavingsRecordService : IMemberSavingsRecordService
{
    private static readonly MemberSavingsRecord Demo = new()
    {
        Name = "Demo User",
        Active = true,
        Age = 47,
        Salary = 42_000m,
        AccruedPension = 8_400m,
        AccruedLump = 25_200m,
        DcPot = 12_600m
    };

    public MemberSavingsRecord GetForUser(string userId) => Demo;
}

public interface IMemberSavingsRecordService
{
    MemberSavingsRecord GetForUser(string userId);
}
