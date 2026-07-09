using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using UmbracoPrism.Shared.Models.Workflow.Calculations;
using UmbracoPrism.Shared.Services.Calculations;

namespace UmbracoPrism.Core.Tests.Workflow.Calculations;

/// <summary>
/// Runs the shared conformance fixtures against the C# evaluator. The same fixture file
/// is executed by the TypeScript evaluator (src/UmbracoPrism.Client) — any behavioural
/// drift between the two runtimes must show up here or there as a failure.
/// </summary>
public class CalculationGoldenTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static TheoryData<string> CaseNames()
    {
        var data = new TheoryData<string>();
        foreach (var testCase in LoadCases())
        {
            data.Add(testCase.GetProperty("name").GetString()!);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void GoldenCase(string name)
    {
        var testCase = LoadCases().Single(c => c.GetProperty("name").GetString() == name);

        var calculations = BuildCalculationSet(testCase);
        var inputs = testCase.TryGetProperty("inputs", out var inputsElement)
            ? (IReadOnlyDictionary<string, object?>)ToScopeValue(inputsElement)!
            : new Dictionary<string, object?>();

        var expectError = testCase.TryGetProperty("expectError", out var errorElement) && errorElement.GetBoolean();

        if (expectError)
        {
            var act = () => new CalculationEvaluator().Evaluate(calculations, inputs);
            act.Should().Throw<CalculationException>(because: $"case '{name}' declares expectError");
            return;
        }

        var result = new CalculationEvaluator().Evaluate(calculations, inputs);

        if (testCase.TryGetProperty("expect", out var expectSingle))
        {
            AssertValue(result.Fields["result"], expectSingle, $"{name} → result");
        }

        if (testCase.TryGetProperty("expectFields", out var expectFields))
        {
            foreach (var expected in expectFields.EnumerateObject())
            {
                result.Fields.Should().ContainKey(expected.Name, because: $"case '{name}' expects field '{expected.Name}'");
                AssertValue(result.Fields[expected.Name], expected.Value, $"{name} → {expected.Name}");
            }
        }

        if (testCase.TryGetProperty("expectSeries", out var expectSeries))
        {
            foreach (var expected in expectSeries.EnumerateObject())
            {
                result.Series.Should().ContainKey(expected.Name);
                var rows = result.Series[expected.Name];
                var expectedRows = expected.Value.EnumerateArray().ToArray();
                rows.Should().HaveCount(expectedRows.Length, because: $"case '{name}' series '{expected.Name}' row count");

                for (var i = 0; i < expectedRows.Length; i++)
                {
                    foreach (var column in expectedRows[i].EnumerateObject())
                    {
                        AssertValue(rows[i][column.Name], column.Value, $"{name} → {expected.Name}[{i}].{column.Name}");
                    }
                }
            }
        }
    }

    private static void AssertValue(object? actual, JsonElement expected, string context)
    {
        switch (expected.ValueKind)
        {
            case JsonValueKind.True:
            case JsonValueKind.False:
                actual.Should().Be(expected.GetBoolean(), because: context);
                break;

            case JsonValueKind.String when actual is decimal actualNumber:
                // Numbers are asserted as invariant strings compared by value, so a result
                // of 1.0m and an expectation of "1" are equal.
                decimal.Parse(expected.GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture)
                    .Should().Be(actualNumber, because: context);
                break;

            case JsonValueKind.String:
                actual.Should().Be(expected.GetString(), because: context);
                break;

            default:
                throw new InvalidOperationException($"Unsupported expectation kind {expected.ValueKind} in {context}.");
        }
    }

    private static WorkflowCalculationSet BuildCalculationSet(JsonElement testCase)
    {
        // Single-expression sugar: { "expr": "1 + 2" } becomes a set with one field "result".
        if (testCase.TryGetProperty("expr", out var expr))
        {
            return new WorkflowCalculationSet
            {
                Tables = testCase.TryGetProperty("tables", out var sugarTables)
                    ? JsonSerializer.Deserialize<Dictionary<string, WorkflowCalculationTable>>(sugarTables.GetRawText(), JsonOptions)
                    : null,
                Fields = new Dictionary<string, WorkflowCalculationField>
                {
                    ["result"] = new() { Expr = expr.GetString() }
                }
            };
        }

        return new WorkflowCalculationSet
        {
            Tables = testCase.TryGetProperty("tables", out var tables)
                ? JsonSerializer.Deserialize<Dictionary<string, WorkflowCalculationTable>>(tables.GetRawText(), JsonOptions)
                : null,
            Fields = testCase.TryGetProperty("fields", out var fields)
                ? JsonSerializer.Deserialize<Dictionary<string, WorkflowCalculationField>>(fields.GetRawText(), JsonOptions)!
                : new Dictionary<string, WorkflowCalculationField>(),
            Series = testCase.TryGetProperty("series", out var series)
                ? JsonSerializer.Deserialize<Dictionary<string, WorkflowCalculationSeries>>(series.GetRawText(), JsonOptions)
                : null
        };
    }

    private static object? ToScopeValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number => element.GetDecimal(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Object => element.EnumerateObject()
            .ToDictionary(p => p.Name, p => ToScopeValue(p.Value)) as IReadOnlyDictionary<string, object?>,
        _ => throw new InvalidOperationException($"Unsupported input kind {element.ValueKind}.")
    };

    private static IReadOnlyList<JsonElement> LoadCases()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FindFixtures()));
        return document.RootElement.GetProperty("cases").EnumerateArray().Select(c => c.Clone()).ToList();
    }

    private static string FindFixtures()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src", "UmbracoPrism.Shared", "calculation-fixtures", "calculation-golden.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("calculation-golden.json not found walking up from test bin.");
    }
}
