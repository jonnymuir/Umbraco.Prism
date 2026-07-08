namespace UmbracoPrism.MockBusinessApp.Services.MoneyModeller;

/// <summary>
/// A member's pension record as held by the (mock) scheme administration system.
/// Monetary values are per-year (pension) or absolute (lump/pot), in today's money.
/// </summary>
public sealed record MemberRecord
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
/// Mock member data source. Real deployments would call the scheme administration
/// system here; the demo maps known users onto three personas covering the
/// active DB+DC, active DB-only and deferred member shapes.
/// </summary>
public class MemberRecordService
{
    private static readonly MemberRecord ActiveWithDc = new()
    {
        Name = "Dr Sarah Mitchell",
        Active = true,
        Age = 47,
        Salary = 82_000m,
        AccruedPension = 16_400m,
        AccruedLump = 49_200m,
        DcPot = 48_300m
    };

    private static readonly MemberRecord ActiveDbOnly = new()
    {
        Name = "James Okafor",
        Active = true,
        Age = 39,
        Salary = 46_000m,
        AccruedPension = 7_800m,
        AccruedLump = 23_400m,
        DcPot = 0m
    };

    private static readonly MemberRecord Deferred = new()
    {
        Name = "Prof Anne Whitfield",
        Active = false,
        Age = 54,
        Salary = 0m,
        AccruedPension = 9_600m,
        AccruedLump = 28_800m,
        DcPot = 21_000m
    };

    public MemberRecord GetForUser(string userId)
    {
        if (userId.Contains("james", StringComparison.OrdinalIgnoreCase)
            || userId.Contains("okafor", StringComparison.OrdinalIgnoreCase))
        {
            return ActiveDbOnly;
        }

        if (userId.Contains("anne", StringComparison.OrdinalIgnoreCase)
            || userId.Contains("whitfield", StringComparison.OrdinalIgnoreCase)
            || userId.Contains("deferred", StringComparison.OrdinalIgnoreCase))
        {
            return Deferred;
        }

        return ActiveWithDc;
    }
}
