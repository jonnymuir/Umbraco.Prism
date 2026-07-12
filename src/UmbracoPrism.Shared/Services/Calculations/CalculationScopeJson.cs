using System.Text.Json;

namespace UmbracoPrism.Shared.Services.Calculations;

/// <summary>
/// Converts a JSON document into calculation-scope values: <see cref="decimal"/> for numbers,
/// nested <see cref="IReadOnlyDictionary{TKey,TValue}"/> for objects (so dotted identifier paths
/// like <c>member.age</c> resolve), and plain <see cref="string"/>/<see cref="bool"/>/<c>null</c>
/// otherwise. Used wherever a scope needs to arrive over the wire — MCP tool arguments, REST
/// request bodies — since <see cref="JsonSerializer"/> alone would box leaf values as
/// <see cref="JsonElement"/>, which the evaluator's <c>ResolvePath</c>/<c>ToDecimal</c>/<c>ToBool</c>
/// don't understand.
/// </summary>
public static class CalculationScopeJson
{
    public static IReadOnlyDictionary<string, object?> ToScopeValues(string json)
    {
        using var document = JsonDocument.Parse(json);
        return (IReadOnlyDictionary<string, object?>)ToScopeValue(document.RootElement)!;
    }

    public static object? ToScopeValue(JsonElement element) => element.ValueKind switch
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
}
