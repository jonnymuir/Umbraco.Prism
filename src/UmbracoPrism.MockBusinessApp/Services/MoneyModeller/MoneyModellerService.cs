using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using UmbracoPrism.Shared.Models.Workflow.Calculations;
using UmbracoPrism.Shared.Services.Calculations;

namespace UmbracoPrism.MockBusinessApp.Services.MoneyModeller;

/// <summary>
/// Host glue for the money-modeller workflow. Contains no projection maths: the maths
/// is the <c>calculations</c> block in <c>money-modeller.json</c>, evaluated by the
/// shared <see cref="CalculationEvaluator"/>. This service supplies the declared
/// service-sourced input (the member record), turns raw form field values into a typed
/// evaluation scope, and packages results for rendering and for the client island.
/// </summary>
public class MoneyModellerService
{
    private static readonly CultureInfo Gb = CultureInfo.GetCultureInfo("en-GB");
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private const decimal DefaultRetireAge = 66m;
    private const decimal DefaultInflation = 2.5m;
    private const decimal DefaultSalaryGrowth = 3m;
    private const decimal DefaultInvReturn = 5m;
    private const string DefaultBenefitOption = "Standard benefits";
    private const string DefaultMoneyBasis = "Today's money";

    private readonly MemberRecordService _members;
    private readonly CalculationEvaluator _evaluator = new();

    public MoneyModellerService(MemberRecordService members)
    {
        _members = members;
    }

    public MemberRecord GetMember(string userId) => _members.GetForUser(userId);

    public static string FormatGbp(decimal value) =>
        string.Create(Gb, $"£{Math.Round(value, 0, MidpointRounding.AwayFromZero):N0}");

    /// <summary>
    /// Evaluates the workflow's calculation set for this member and the instance's
    /// current field values. The returned fields/series are the authoritative figures.
    /// </summary>
    public CalculationResult Evaluate(
        WorkflowCalculationSet calculations,
        MemberRecord member,
        IReadOnlyDictionary<string, object?> fieldValues)
    {
        return _evaluator.Evaluate(calculations, BuildScope(member, fieldValues));
    }

    /// <summary>
    /// Builds the typed evaluation scope: parsed form inputs (with scheme defaults for
    /// anything not yet answered) plus the service-sourced member group.
    /// </summary>
    public IReadOnlyDictionary<string, object?> BuildScope(
        MemberRecord member,
        IReadOnlyDictionary<string, object?> fieldValues)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["retireAge"] = ReadDecimal(fieldValues, "retireAge")
                ?? ReadDecimal(fieldValues, "qAge")
                ?? DefaultRetireAge,
            ["benefitOption"] = ReadString(fieldValues, "benefitOption") ?? DefaultBenefitOption,
            ["inflation"] = ReadDecimal(fieldValues, "inflation") ?? DefaultInflation,
            ["salaryGrowth"] = ReadDecimal(fieldValues, "salaryGrowth") ?? DefaultSalaryGrowth,
            ["invReturn"] = ReadDecimal(fieldValues, "invReturn") ?? DefaultInvReturn,
            ["moneyBasis"] = ReadString(fieldValues, "moneyBasis") ?? DefaultMoneyBasis,
            ["qPension"] = ReadDecimal(fieldValues, "qPension") ?? 0m,
            ["qLump"] = ReadDecimal(fieldValues, "qLump") ?? 0m,
            ["qDC"] = ReadDecimal(fieldValues, "qDC") ?? 0m,
            ["qAge"] = ReadDecimal(fieldValues, "qAge") ?? DefaultRetireAge,
            ["member"] = new Dictionary<string, object?>(StringComparer.Ordinal)
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
    }

    /// <summary>
    /// Builds the render-data model for the prism-money-modeller island: the member,
    /// the typed inputs, the calculation set itself (the client evaluates the same
    /// definitions live), and the server-evaluated results for first paint.
    /// </summary>
    public JsonObject BuildModelData(
        WorkflowCalculationSet calculations,
        MemberRecord member,
        IReadOnlyDictionary<string, object?> scope,
        CalculationResult result)
    {
        var model = new JsonObject
        {
            ["member"] = ToJson(scope["member"]),
            ["inputs"] = new JsonObject
            {
                ["retireAge"] = ToJson(scope["retireAge"]),
                ["benefitOption"] = ToJson(scope["benefitOption"]),
                ["inflation"] = ToJson(scope["inflation"]),
                ["salaryGrowth"] = ToJson(scope["salaryGrowth"]),
                ["invReturn"] = ToJson(scope["invReturn"]),
                ["moneyBasis"] = ToJson(scope["moneyBasis"]),
                ["qPension"] = ToJson(scope["qPension"]),
                ["qLump"] = ToJson(scope["qLump"]),
                ["qDC"] = ToJson(scope["qDC"]),
                ["qAge"] = ToJson(scope["qAge"])
            },
            ["calculations"] = JsonSerializer.SerializeToNode(calculations, CamelCase),
            ["results"] = new JsonObject()
        };

        var results = (JsonObject)model["results"]!;
        foreach (var (name, value) in result.Fields)
        {
            results[name] = ToJson(value);
        }

        var chart = new JsonArray();
        if (result.Series.TryGetValue("incomeByAge", out var rows))
        {
            foreach (var row in rows)
            {
                var entry = new JsonObject();
                foreach (var (column, value) in row)
                {
                    entry[column] = ToJson(value);
                }

                chart.Add(entry);
            }
        }

        model["chart"] = chart;
        return model;
    }

    private static JsonNode? ToJson(object? value) => value switch
    {
        null => null,
        decimal d => JsonValue.Create(d),
        bool b => JsonValue.Create(b),
        string s => JsonValue.Create(s),
        IReadOnlyDictionary<string, object?> map => new JsonObject(
            map.Select(pair => new KeyValuePair<string, JsonNode?>(pair.Key, ToJson(pair.Value)))),
        _ => JsonValue.Create(value.ToString())
    };

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
