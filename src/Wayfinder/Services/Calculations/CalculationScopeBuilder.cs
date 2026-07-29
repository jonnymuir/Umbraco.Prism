using System.Globalization;
using Wayfinder.Extensions;
using Wayfinder.Models.ServiceDesign;
using Wayfinder.Models.ServiceDesign.Components;

namespace Wayfinder.Services.Calculations;

/// <summary>
/// Builds the typed evaluation scope for a blueprint's calculation set from raw instance
/// field values. Types and defaults come from the definition's own input components:
/// slider/number/decimal fields parse as decimals (tolerating "£" and thousands
/// separators written back for display), everything else stays a string. Service-sourced
/// values supplied by the host are merged in last.
///
/// The TypeScript live-form runtime applies the same rules client-side; keep the two in
/// step (the input-type map is shipped to the client in the live model for that reason).
/// </summary>
public static class CalculationScopeBuilder
{
    private static readonly string[] NumericFieldTypes = ["slider", "number", "decimal", "currency"];

    /// <summary>Returns fieldKey → ("number" | "string", default) for every input in the definition.</summary>
    public static IReadOnlyDictionary<string, (string Type, string? Default)> DescribeInputs(
        ServiceBlueprint definition)
    {
        var inputs = new Dictionary<string, (string, string?)>(StringComparer.Ordinal);
        foreach (var stage in definition.Stages)
        {
            foreach (var input in stage.Components.GetAllInputs())
            {
                if (string.IsNullOrWhiteSpace(input.FieldKey) || inputs.ContainsKey(input.FieldKey))
                {
                    continue;
                }

                var isNumeric = input is SliderComponent or NumberInputComponent or DecimalInputComponent;
                inputs[input.FieldKey] = (isNumeric ? "number" : "string", input.Default);
            }
        }

        return inputs;
    }

    public static Dictionary<string, object?> Build(
        ServiceBlueprint definition,
        IReadOnlyDictionary<string, object?> fieldValues,
        IReadOnlyDictionary<string, object?>? serviceInputs = null)
    {
        var scope = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var (fieldKey, (type, defaultValue)) in DescribeInputs(definition))
        {
            var raw = fieldValues.TryGetValue(fieldKey, out var saved) ? saved?.ToString() : null;
            if (string.IsNullOrWhiteSpace(raw))
            {
                raw = defaultValue;
            }

            if (raw is null)
            {
                continue; // absent and no default — expressions referencing it will error clearly
            }

            scope[fieldKey] = type == "number" ? ParseNumeric(raw, fieldKey) : raw;
        }

        foreach (var (key, value) in serviceInputs ?? new Dictionary<string, object?>())
        {
            scope[key] = value;
        }

        return scope;
    }

    private static object? ParseNumeric(string raw, string fieldKey)
    {
        var cleaned = raw.Replace("£", "").Replace(",", "").Trim();
        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}
