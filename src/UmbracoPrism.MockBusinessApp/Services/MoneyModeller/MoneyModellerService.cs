using System.Globalization;
using System.Text.Json.Nodes;

namespace UmbracoPrism.MockBusinessApp.Services.MoneyModeller;

/// <summary>
/// Scheme parameters driving the money modeller projection. In a multi-tenant
/// deployment these would be tenant/scheme configuration, not code.
/// </summary>
public sealed record SchemeParameters
{
    public decimal AccrualDivisor { get; init; } = 75m;
    public decimal LumpAccrualFactor { get; init; } = 3m;
    public decimal SalaryThreshold { get; init; } = 74_208m;
    public int NormalPensionAge { get; init; } = 66;
    public int MinRetirementAge { get; init; } = 55;
    public int MaxRetirementAge { get; init; } = 75;
    public decimal EarlyPensionReductionPerYear { get; init; } = 0.04m;
    public decimal EarlyLumpReductionPerYear { get; init; } = 0.025m;
    public decimal LateUpliftPerYear { get; init; } = 0.03m;
    public decimal CommutationRate { get; init; } = 12m;
    public decimal TaxFreeShare { get; init; } = 0.25m;
    public int DcDrawdownYears { get; init; } = 20;
    public decimal StatePensionAmount { get; init; } = 11_975m;
    public int StatePensionAge { get; init; } = 68;
    public decimal AboveThresholdDcRate { get; init; } = 0.2m;
    public decimal DefaultInflation { get; init; } = 2.5m;
    public decimal DefaultSalaryGrowth { get; init; } = 3m;
    public decimal DefaultInvReturn { get; init; } = 5m;
}

/// <summary>Inputs for a single scenario computation.</summary>
public sealed record ScenarioInputs
{
    public int RetireAge { get; init; } = 66;
    public string BenefitOption { get; init; } = MoneyModellerService.OptionStandard;
    public decimal Inflation { get; init; } = 2.5m;
    public decimal SalaryGrowth { get; init; } = 3m;
    public decimal InvReturn { get; init; } = 5m;
    public bool TodaysMoney { get; init; } = true;
    public bool QuoteMode { get; init; }
    public decimal QuotePension { get; init; }
    public decimal QuoteLump { get; init; }
    public decimal QuoteDc { get; init; }
}

/// <summary>Authoritative results for one scenario.</summary>
public sealed record ScenarioResult
{
    public decimal Pension { get; init; }
    public decimal Cash { get; init; }
    public string CashLabel { get; init; } = "Tax-free cash";
    public decimal DcIncome { get; init; }
    public decimal RemainingPot { get; init; }
    public decimal Total { get; init; }
    public IReadOnlyList<ChartPoint> Chart { get; init; } = Array.Empty<ChartPoint>();
}

/// <summary>One bar of the income-by-age chart.</summary>
public sealed record ChartPoint(int Age, decimal Db, decimal Dc, decimal Sp);

/// <summary>
/// The scheme's projection engine for the money modeller. This is the single source
/// of truth for scenario figures: the client island shows indicative live numbers,
/// but every rendered/committed figure is recomputed here.
/// </summary>
public class MoneyModellerService
{
    public const string OptionStandard = "Standard benefits";
    public const string OptionMaxTfc = "Maximum tax-free cash";
    public const string OptionDcCash = "Take DC pot as cash";

    private static readonly CultureInfo Gb = CultureInfo.GetCultureInfo("en-GB");

    private readonly MemberRecordService _members;
    private readonly SchemeParameters _parameters = new();

    public MoneyModellerService(MemberRecordService members)
    {
        _members = members;
    }

    public SchemeParameters Parameters => _parameters;

    public MemberRecord GetMember(string userId) => _members.GetForUser(userId);

    public static string FormatGbp(decimal value) =>
        string.Create(Gb, $"£{Math.Round(value):N0}");

    public ScenarioResult Compute(MemberRecord member, ScenarioInputs inputs)
    {
        var p = _parameters;
        var retireAge = Math.Clamp(inputs.RetireAge, Math.Max(p.MinRetirementAge, member.Age + 1), p.MaxRetirementAge);
        var years = Math.Max(0, retireAge - member.Age);
        var realGrowth = (double)(inputs.SalaryGrowth - inputs.Inflation) / 100d;
        var realReturn = (double)(inputs.InvReturn - inputs.Inflation) / 100d;

        double basePension, baseLump, pot;
        if (inputs.QuoteMode)
        {
            basePension = (double)inputs.QuotePension;
            baseLump = (double)inputs.QuoteLump;
            pot = (double)inputs.QuoteDc;
        }
        else
        {
            var cappedSalary = (double)Math.Min(member.Salary, p.SalaryThreshold);
            var futurePension = member.Active
                ? years * (cappedSalary / (double)p.AccrualDivisor) * Math.Pow(1 + Math.Max(realGrowth, -0.05), years / 2.0)
                : 0d;
            basePension = (double)member.AccruedPension + futurePension;
            baseLump = (double)member.AccruedLump + (double)p.LumpAccrualFactor * futurePension;

            var annualDc = member.Active
                ? Math.Max(0d, (double)(member.Salary - p.SalaryThreshold)) * (double)p.AboveThresholdDcRate
                : 0d;
            var growth = Math.Pow(1 + realReturn, years);
            pot = (double)member.DcPot * growth
                  + (Math.Abs(realReturn) > 0.0001 ? annualDc * ((growth - 1) / realReturn) : annualDc * years);
        }

        // Early/late retirement factors apply to projected benefits only — a quote is age-specific.
        var pension = basePension;
        var lump = baseLump;
        if (!inputs.QuoteMode)
        {
            if (retireAge < p.NormalPensionAge)
            {
                var earlyYears = p.NormalPensionAge - retireAge;
                pension *= Math.Max(0.4, 1 - (double)p.EarlyPensionReductionPerYear * earlyYears);
                lump *= Math.Max(0.5, 1 - (double)p.EarlyLumpReductionPerYear * earlyYears);
            }
            else if (retireAge > p.NormalPensionAge)
            {
                pension *= 1 + (double)p.LateUpliftPerYear * (Math.Min(retireAge, p.MaxRetirementAge) - p.NormalPensionAge);
            }
        }

        var moneyFactor = inputs.TodaysMoney ? 1d : Math.Pow(1 + (double)inputs.Inflation / 100d, years);
        pension *= moneyFactor;
        lump *= moneyFactor;
        pot *= moneyFactor;

        var totalValue = 20 * pension + lump + pot;
        var maxTfc = (double)p.TaxFreeShare * totalValue;

        var resultPension = pension;
        var resultCash = lump;
        var remainingPot = pot;
        var cashLabel = "Tax-free cash";

        if (inputs.BenefitOption == OptionMaxTfc)
        {
            var extra = Math.Max(0d, maxTfc - lump);
            var fromDc = Math.Min(pot, extra);
            var shortfall = extra - fromDc;
            resultPension = Math.Max(0d, pension - shortfall / (double)p.CommutationRate);
            resultCash = maxTfc;
            remainingPot = pot - fromDc;
        }
        else if (inputs.BenefitOption == OptionDcCash)
        {
            resultCash = lump + pot;
            remainingPot = 0d;
            cashLabel = "One-off cash";
        }

        var dcIncome = remainingPot / p.DcDrawdownYears;
        var statePension = (double)p.StatePensionAmount * moneyFactor;
        var total = resultPension + dcIncome + (retireAge >= p.StatePensionAge ? statePension : 0d);

        var chart = new List<ChartPoint>();
        for (var age = retireAge; age <= 90; age++)
        {
            chart.Add(new ChartPoint(
                age,
                Round(resultPension),
                age < retireAge + p.DcDrawdownYears ? Round(dcIncome) : 0m,
                age >= p.StatePensionAge ? Round(statePension) : 0m));
        }

        return new ScenarioResult
        {
            Pension = Round(resultPension),
            Cash = Round(resultCash),
            CashLabel = cashLabel,
            DcIncome = Round(dcIncome),
            RemainingPot = Round(remainingPot),
            Total = Round(total),
            Chart = chart
        };
    }

    /// <summary>
    /// Reads scenario inputs from workflow instance field values, falling back to
    /// scheme defaults. Quote mode is active when the quote-entry path populated qPension.
    /// </summary>
    public ScenarioInputs ReadInputs(MemberRecord member, IReadOnlyDictionary<string, object?> fieldValues)
    {
        var quotePension = ReadDecimal(fieldValues, "qPension") ?? 0m;
        var quoteMode = quotePension > 0m;
        var defaultAge = quoteMode
            ? (int)(ReadDecimal(fieldValues, "qAge") ?? _parameters.NormalPensionAge)
            : _parameters.NormalPensionAge;

        return new ScenarioInputs
        {
            RetireAge = (int)(ReadDecimal(fieldValues, "retireAge") ?? defaultAge),
            BenefitOption = ReadString(fieldValues, "benefitOption") ?? OptionStandard,
            Inflation = ReadDecimal(fieldValues, "inflation") ?? _parameters.DefaultInflation,
            SalaryGrowth = ReadDecimal(fieldValues, "salaryGrowth") ?? _parameters.DefaultSalaryGrowth,
            InvReturn = ReadDecimal(fieldValues, "invReturn") ?? _parameters.DefaultInvReturn,
            TodaysMoney = !string.Equals(ReadString(fieldValues, "moneyBasis"), "Future money", StringComparison.OrdinalIgnoreCase),
            QuoteMode = quoteMode,
            QuotePension = quotePension,
            QuoteLump = ReadDecimal(fieldValues, "qLump") ?? 0m,
            QuoteDc = ReadDecimal(fieldValues, "qDC") ?? 0m
        };
    }

    /// <summary>Builds the render-data model consumed by the prism-money-modeller island.</summary>
    public JsonObject BuildModelData(MemberRecord member, ScenarioInputs inputs, ScenarioResult result)
    {
        var p = _parameters;

        var chart = new JsonArray();
        foreach (var point in result.Chart)
        {
            chart.Add(new JsonObject
            {
                ["age"] = point.Age,
                ["db"] = point.Db,
                ["dc"] = point.Dc,
                ["sp"] = point.Sp
            });
        }

        return new JsonObject
        {
            ["member"] = new JsonObject
            {
                ["name"] = member.Name,
                ["active"] = member.Active,
                ["age"] = member.Age,
                ["salary"] = member.Salary,
                ["accruedPension"] = member.AccruedPension,
                ["accruedLump"] = member.AccruedLump,
                ["dcPot"] = member.DcPot
            },
            ["parameters"] = new JsonObject
            {
                ["accrualDivisor"] = p.AccrualDivisor,
                ["lumpAccrualFactor"] = p.LumpAccrualFactor,
                ["salaryThreshold"] = p.SalaryThreshold,
                ["normalPensionAge"] = p.NormalPensionAge,
                ["minRetirementAge"] = Math.Max(p.MinRetirementAge, member.Age + 1),
                ["maxRetirementAge"] = p.MaxRetirementAge,
                ["earlyPensionReductionPerYear"] = p.EarlyPensionReductionPerYear,
                ["earlyLumpReductionPerYear"] = p.EarlyLumpReductionPerYear,
                ["lateUpliftPerYear"] = p.LateUpliftPerYear,
                ["commutationRate"] = p.CommutationRate,
                ["taxFreeShare"] = p.TaxFreeShare,
                ["dcDrawdownYears"] = p.DcDrawdownYears,
                ["statePensionAmount"] = p.StatePensionAmount,
                ["statePensionAge"] = p.StatePensionAge,
                ["aboveThresholdDcRate"] = p.AboveThresholdDcRate
            },
            ["inputs"] = new JsonObject
            {
                ["retireAge"] = inputs.RetireAge,
                ["benefitOption"] = inputs.BenefitOption,
                ["inflation"] = inputs.Inflation,
                ["salaryGrowth"] = inputs.SalaryGrowth,
                ["invReturn"] = inputs.InvReturn,
                ["todaysMoney"] = inputs.TodaysMoney,
                ["quoteMode"] = inputs.QuoteMode,
                ["quotePension"] = inputs.QuotePension,
                ["quoteLump"] = inputs.QuoteLump,
                ["quoteDc"] = inputs.QuoteDc
            },
            ["results"] = new JsonObject
            {
                ["pension"] = result.Pension,
                ["cash"] = result.Cash,
                ["cashLabel"] = result.CashLabel,
                ["dcIncome"] = result.DcIncome,
                ["total"] = result.Total
            },
            ["chart"] = chart
        };
    }

    private static decimal Round(double value) => Math.Round((decimal)value, 0, MidpointRounding.AwayFromZero);

    private static string? ReadString(IReadOnlyDictionary<string, object?> fieldValues, string key) =>
        fieldValues.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static decimal? ReadDecimal(IReadOnlyDictionary<string, object?> fieldValues, string key)
    {
        var raw = ReadString(fieldValues, key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        raw = raw.Replace("£", "").Replace(",", "").Trim();
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}
