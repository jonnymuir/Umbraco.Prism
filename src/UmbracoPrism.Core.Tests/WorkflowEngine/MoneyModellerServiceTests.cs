using FluentAssertions;
using UmbracoPrism.MockBusinessApp.Services.MoneyModeller;

namespace UmbracoPrism.Core.Tests.WorkflowEngine;

public class MoneyModellerServiceTests
{
    private readonly MoneyModellerService _service = new(new MemberRecordService());

    private static MemberRecord ActiveWithDc => new()
    {
        Name = "Dr Sarah Mitchell",
        Active = true,
        Age = 47,
        Salary = 82_000m,
        AccruedPension = 16_400m,
        AccruedLump = 49_200m,
        DcPot = 48_300m
    };

    private static MemberRecord Deferred => new()
    {
        Name = "Prof Anne Whitfield",
        Active = false,
        Age = 54,
        Salary = 0m,
        AccruedPension = 9_600m,
        AccruedLump = 28_800m,
        DcPot = 21_000m
    };

    [Fact]
    public void StandardBenefitsAtNormalPensionAge_ProjectsAboveAccruedBenefits()
    {
        var result = _service.Compute(ActiveWithDc, new ScenarioInputs { RetireAge = 66 });

        result.Pension.Should().BeGreaterThan(ActiveWithDc.AccruedPension);
        result.Cash.Should().BeGreaterThan(ActiveWithDc.AccruedLump);
        result.CashLabel.Should().Be("Tax-free cash");
        result.DcIncome.Should().BeGreaterThan(0, "the DC pot funds a drawdown income");
        Math.Abs(result.Total - (result.Pension + result.DcIncome)).Should().BeLessThanOrEqualTo(1m,
            "at 66 the State Pension has not started, so it is excluded from income at retirement age");
    }

    [Fact]
    public void TotalIncome_IncludesStatePensionOnlyFromStatePensionAge()
    {
        var at66 = _service.Compute(ActiveWithDc, new ScenarioInputs { RetireAge = 66 });
        var at68 = _service.Compute(ActiveWithDc, new ScenarioInputs { RetireAge = 68 });

        at68.Total.Should().Be(at68.Pension + at68.DcIncome + 11_975m);
        at66.Chart.First(point => point.Age == 66).Sp.Should().Be(0);
        at66.Chart.First(point => point.Age == 68).Sp.Should().Be(11_975m);
    }

    [Fact]
    public void EarlyRetirement_ReducesPensionAndLump()
    {
        var atNpa = _service.Compute(ActiveWithDc, new ScenarioInputs { RetireAge = 66 });
        var early = _service.Compute(ActiveWithDc, new ScenarioInputs { RetireAge = 60 });

        early.Pension.Should().BeLessThan(atNpa.Pension);
        early.Cash.Should().BeLessThan(atNpa.Cash);
    }

    [Fact]
    public void LateRetirement_UpliftsPension()
    {
        var atNpa = _service.Compute(ActiveWithDc, new ScenarioInputs { RetireAge = 66 });
        var late = _service.Compute(ActiveWithDc, new ScenarioInputs { RetireAge = 70 });

        late.Pension.Should().BeGreaterThan(atNpa.Pension);
    }

    [Fact]
    public void MaximumTaxFreeCash_TakesQuarterOfTotalValue()
    {
        var standard = _service.Compute(ActiveWithDc, new ScenarioInputs { RetireAge = 66 });
        var maxTfc = _service.Compute(ActiveWithDc, new ScenarioInputs
        {
            RetireAge = 66,
            BenefitOption = MoneyModellerService.OptionMaxTfc
        });

        maxTfc.Cash.Should().BeGreaterThan(standard.Cash);
        maxTfc.Pension.Should().BeLessThanOrEqualTo(standard.Pension);
        maxTfc.RemainingPot.Should().BeLessThan(standard.RemainingPot,
            "extra tax-free cash is taken from the DC pot before commuting pension");
    }

    [Fact]
    public void DcPotAsCash_KeepsFullPensionAndEmptiesThePot()
    {
        var standard = _service.Compute(ActiveWithDc, new ScenarioInputs { RetireAge = 66 });
        var dcCash = _service.Compute(ActiveWithDc, new ScenarioInputs
        {
            RetireAge = 66,
            BenefitOption = MoneyModellerService.OptionDcCash
        });

        dcCash.Pension.Should().Be(standard.Pension);
        dcCash.Cash.Should().BeGreaterThan(standard.Cash);
        dcCash.DcIncome.Should().Be(0);
        dcCash.CashLabel.Should().Be("One-off cash");
    }

    [Fact]
    public void FutureMoney_InflatesFiguresRelativeToTodaysMoney()
    {
        var todays = _service.Compute(ActiveWithDc, new ScenarioInputs { RetireAge = 66, TodaysMoney = true });
        var future = _service.Compute(ActiveWithDc, new ScenarioInputs { RetireAge = 66, TodaysMoney = false });

        future.Pension.Should().BeGreaterThan(todays.Pension);
        future.Cash.Should().BeGreaterThan(todays.Cash);
    }

    [Fact]
    public void DeferredMember_AccruesNoFurtherDbBenefits()
    {
        var result = _service.Compute(Deferred, new ScenarioInputs { RetireAge = 66 });

        result.Pension.Should().Be(Deferred.AccruedPension,
            "a deferred member at NPA gets exactly the accrued pension — no future accrual, no reduction");
        result.DcIncome.Should().BeGreaterThan(0, "the deferred DC pot stays invested");
    }

    [Fact]
    public void QuoteMode_UsesQuoteFiguresWithoutRetirementFactors()
    {
        var result = _service.Compute(ActiveWithDc, new ScenarioInputs
        {
            RetireAge = 60,
            QuoteMode = true,
            QuotePension = 18_500m,
            QuoteLump = 55_500m,
            QuoteDc = 48_000m
        });

        result.Pension.Should().Be(18_500m, "quote figures are age-specific, so no early-retirement reduction applies");
        result.Cash.Should().Be(55_500m);
        result.DcIncome.Should().Be(2_400m, "48,000 drawn over 20 years");
    }

    [Fact]
    public void ReadInputs_FallsBackToSchemeDefaults_AndDetectsQuoteMode()
    {
        var member = ActiveWithDc;

        var defaults = _service.ReadInputs(member, new Dictionary<string, object?>());
        defaults.RetireAge.Should().Be(66);
        defaults.BenefitOption.Should().Be(MoneyModellerService.OptionStandard);
        defaults.Inflation.Should().Be(2.5m);
        defaults.QuoteMode.Should().BeFalse();

        var quote = _service.ReadInputs(member, new Dictionary<string, object?>
        {
            ["qPension"] = "18500",
            ["qLump"] = "55500",
            ["qAge"] = "63"
        });
        quote.QuoteMode.Should().BeTrue();
        quote.RetireAge.Should().Be(63, "quote mode defaults the retirement age to the quoted age");
    }

    [Fact]
    public void ReadInputs_ParsesFormattedCurrencyValuesWrittenBackByTheEngine()
    {
        var inputs = _service.ReadInputs(ActiveWithDc, new Dictionary<string, object?>
        {
            ["retireAge"] = "60",
            ["inflation"] = "3.5"
        });

        inputs.RetireAge.Should().Be(60);
        inputs.Inflation.Should().Be(3.5m);
    }
}
