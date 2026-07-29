using System.Text.Json;
using FluentAssertions;
using UmbracoPrism.MockBusinessApp.Services.MoneyModeller;
using Wayfinder.Models.ServiceDesign;
using Wayfinder.Models.ServiceDesign.Calculations;
using Wayfinder.Services.Calculations;
// The maths under test is the declarative block in money-modeller.json; the scope is
// built exactly as the engine builds it (definition input types + service inputs).

namespace UmbracoPrism.Core.Tests.ServiceDesign.ProcessManager;

/// <summary>
/// Behavioural tests for the money-modeller maths as declared in the seed's
/// calculations block — the single source of the projection logic. These evaluate the
/// real money-modeller.json exactly as the engine does (CalculationScopeBuilder +
/// CalculationEvaluator) and assert scheme behaviour, not implementation.
/// </summary>
public class MoneyModellerCalculationTests
{
    private readonly CalculationEvaluator _evaluator = new();
    private readonly ServiceBlueprint _definition = LoadSeedDefinition();

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

    private decimal Field(CalculationResult result, string name) => (decimal)result.Fields[name]!;

    private CalculationResult Evaluate(MemberRecord member, Dictionary<string, object?> fieldValues)
    {
        var serviceInputs = new Dictionary<string, object?>
        {
            ["member"] = new Dictionary<string, object?>
            {
                ["name"] = member.Name,
                ["active"] = member.Active,
                ["age"] = (decimal)member.Age,
                ["salary"] = member.Salary,
                ["accruedPension"] = member.AccruedPension,
                ["accruedLump"] = member.AccruedLump,
                ["dcPot"] = member.DcPot
            }
        };

        var scope = CalculationScopeBuilder.Build(_definition, fieldValues, serviceInputs);
        return _evaluator.Evaluate(_definition.Calculations!, scope);
    }

    [Fact]
    public void StandardBenefitsAtNormalPensionAge_ProjectAboveAccruedBenefits()
    {
        var result = Evaluate(ActiveWithDc, new() { ["retireAge"] = "66" });

        Field(result, "resultPension").Should().BeGreaterThan(ActiveWithDc.AccruedPension);
        Field(result, "resultCash").Should().BeGreaterThan(ActiveWithDc.AccruedLump);
        Field(result, "resultDcIncome").Should().BeGreaterThan(0, "the DC pot funds a drawdown income");
        result.Fields["cashLabel"].Should().Be("Tax-free cash");
    }

    [Fact]
    public void EarlyRetirement_ReducesPensionAndLump_PerTheFactorTables()
    {
        var atNpa = Evaluate(ActiveWithDc, new() { ["retireAge"] = "66" });
        var early = Evaluate(ActiveWithDc, new() { ["retireAge"] = "60" });

        Field(early, "resultPension").Should().BeLessThan(Field(atNpa, "resultPension"));
        Field(early, "resultCash").Should().BeLessThan(Field(atNpa, "resultCash"));
        Field(early, "pensionFactor").Should().Be(0.76m, "the table interpolates 0.04/year between 55 and 66");
        Field(early, "lumpFactor").Should().Be(0.85m, "the table interpolates 0.025/year between 55 and 66");
    }

    [Fact]
    public void LateRetirement_UpliftsPension()
    {
        var atNpa = Evaluate(ActiveWithDc, new() { ["retireAge"] = "66" });
        var late = Evaluate(ActiveWithDc, new() { ["retireAge"] = "70" });

        Field(late, "resultPension").Should().BeGreaterThan(Field(atNpa, "resultPension"));
        Field(late, "pensionFactor").Should().Be(1.12m, "the table interpolates 0.03/year above 66");
    }

    [Fact]
    public void MaximumTaxFreeCash_TakesQuarterOfTotalValue_FromTheDcPotFirst()
    {
        var standard = Evaluate(ActiveWithDc, new() { ["retireAge"] = "66" });
        var maxTfc = Evaluate(ActiveWithDc, new()
        {
            ["retireAge"] = "66",
            ["benefitOption"] = "Maximum tax-free cash"
        });

        Field(maxTfc, "resultCash").Should().BeGreaterThan(Field(standard, "resultCash"));
        Field(maxTfc, "resultCash").Should().Be(Math.Round(Field(maxTfc, "maxTfc"), 0, MidpointRounding.AwayFromZero));
        Field(maxTfc, "resultPension").Should().BeLessThanOrEqualTo(Field(standard, "resultPension"));
        Field(maxTfc, "potOut").Should().BeLessThan(Field(standard, "potOut"),
            "extra tax-free cash comes from the DC pot before commuting pension");
    }

    [Fact]
    public void DcPotAsCash_KeepsFullPensionAndEmptiesThePot()
    {
        var standard = Evaluate(ActiveWithDc, new() { ["retireAge"] = "66" });
        var dcCash = Evaluate(ActiveWithDc, new()
        {
            ["retireAge"] = "66",
            ["benefitOption"] = "Take DC pot as cash"
        });

        Field(dcCash, "resultPension").Should().Be(Field(standard, "resultPension"));
        Field(dcCash, "resultCash").Should().BeGreaterThan(Field(standard, "resultCash"));
        Field(dcCash, "resultDcIncome").Should().Be(0);
        dcCash.Fields["cashLabel"].Should().Be("One-off cash");
    }

    [Fact]
    public void FutureMoney_InflatesFiguresRelativeToTodaysMoney()
    {
        var todays = Evaluate(ActiveWithDc, new() { ["retireAge"] = "66" });
        var future = Evaluate(ActiveWithDc, new() { ["retireAge"] = "66", ["moneyBasis"] = "Future money" });

        Field(future, "resultPension").Should().BeGreaterThan(Field(todays, "resultPension"));
        Field(future, "resultCash").Should().BeGreaterThan(Field(todays, "resultCash"));
    }

    [Fact]
    public void DeferredMember_AccruesNoFurtherDbBenefits()
    {
        var result = Evaluate(Deferred, new() { ["retireAge"] = "66" });

        Field(result, "resultPension").Should().Be(Deferred.AccruedPension,
            "a deferred member at NPA gets exactly the accrued pension — no future accrual, no reduction");
        Field(result, "resultDcIncome").Should().BeGreaterThan(0, "the deferred DC pot stays invested");
    }

    [Fact]
    public void QuoteMode_UsesQuoteFiguresWithoutRetirementFactors()
    {
        var result = Evaluate(ActiveWithDc, new()
        {
            ["qPension"] = "18500",
            ["qLump"] = "55500",
            ["qDC"] = "48000",
            ["qAge"] = "60"
        });

        result.Fields["quoteMode"].Should().Be(true);
        Field(result, "resultPension").Should().Be(18_500m,
            "quote figures are age-specific, so no early-retirement reduction applies");
        Field(result, "resultCash").Should().Be(55_500m);
        Field(result, "resultDcIncome").Should().Be(2_400m, "48,000 drawn over 20 years");
        Field(result, "retireAgeEff").Should().Be(60m, "quote mode pins the retirement age to the quoted age");
    }

    [Fact]
    public void RetirementAge_IsClampedToTheMemberSpecificRange()
    {
        var result = Evaluate(ActiveWithDc, new() { ["retireAge"] = "40" });

        Field(result, "retireAgeEff").Should().Be(55m);
        Field(Evaluate(Deferred, new() { ["retireAge"] = "40" }), "retireAgeEff").Should().Be(55m);
    }

    [Fact]
    public void IncomeSeries_CoversRetirementTo90_WithStatePensionFrom68AndDrawdownFor20Years()
    {
        var result = Evaluate(ActiveWithDc, new() { ["retireAge"] = "60" });
        var rows = result.Series["incomeByAge"];

        rows.Should().HaveCount(31, "ages 60 to 90 inclusive");
        ((decimal)rows[0]["sp"]!).Should().Be(0, "State Pension starts at 68");
        ((decimal)rows.First(r => (decimal)r["age"]! == 68m)["sp"]!).Should().Be(11_975m);
        ((decimal)rows.First(r => (decimal)r["age"]! == 79m)["dc"]!).Should().BeGreaterThan(0, "drawdown runs to 79");
        ((decimal)rows.First(r => (decimal)r["age"]! == 80m)["dc"]!).Should().Be(0, "the 20-year drawdown ends at 80");
    }

    [Fact]
    public void EvaluateCollectingErrors_OnARealSetWithADeliberatelyBrokenField_ReportsRatherThanThrows()
    {
        var serviceInputs = new Dictionary<string, object?>
        {
            ["member"] = new Dictionary<string, object?>
            {
                ["name"] = ActiveWithDc.Name,
                ["active"] = ActiveWithDc.Active,
                ["age"] = (decimal)ActiveWithDc.Age,
                ["salary"] = ActiveWithDc.Salary,
                ["accruedPension"] = ActiveWithDc.AccruedPension,
                ["accruedLump"] = ActiveWithDc.AccruedLump,
                ["dcPot"] = ActiveWithDc.DcPot
            }
        };
        var scope = CalculationScopeBuilder.Build(_definition, new Dictionary<string, object?> { ["retireAge"] = "66" }, serviceInputs);

        var broken = _definition.Calculations! with
        {
            Fields = new Dictionary<string, ServiceBlueprintCalculationField>(_definition.Calculations!.Fields)
            {
                ["basePension"] = new ServiceBlueprintCalculationField { Expr = "thisNameDoesNotExist + 1" }
            }
        };

        var evaluation = _evaluator.EvaluateCollectingErrors(broken, scope);

        evaluation.Diagnostics.Should().ContainSingle(d =>
            d.Kind == CalculationDiagnosticKind.Field && d.Name == "basePension" && d.Message.Contains("Unknown name"));
        evaluation.Result.Fields.Should().NotContainKey("basePension",
            "a failed field is omitted from the result rather than the whole evaluation throwing");
        evaluation.Result.Fields.Should().ContainKey("quoteMode",
            "fields declared before the broken one should still evaluate");
    }

    [Fact]
    public void ScopeParsing_HandlesFormattedValuesWrittenBackByTheEngine()
    {
        var result = Evaluate(ActiveWithDc, new()
        {
            ["retireAge"] = "60",
            ["resultPension"] = "£18,759",
            ["inflation"] = "3.5"
        });

        Field(result, "retireAgeEff").Should().Be(60m);
        Field(result, "realGrowth").Should().Be(-0.005m, "(3 - 3.5) / 100 with the default salary growth");
    }

    private static ServiceBlueprint LoadSeedDefinition()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src", "UmbracoPrism.MockBusinessApp", "service-blueprints", "money-modeller.json");
            if (File.Exists(candidate))
            {
                var definition = JsonSerializer.Deserialize<ServiceBlueprint>(
                    File.ReadAllText(candidate),
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        PropertyNameCaseInsensitive = true,
                        AllowOutOfOrderMetadataProperties = true
                    })!;

                if (definition.Calculations is null)
                {
                    throw new InvalidOperationException("money-modeller.json has no calculations block.");
                }

                return definition;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("money-modeller.json not found walking up from test bin.");
    }
}
