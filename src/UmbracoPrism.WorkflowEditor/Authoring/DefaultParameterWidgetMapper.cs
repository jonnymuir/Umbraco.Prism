namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Default mapping rules from parameter schema metadata to editor widgets.
/// </summary>
public sealed class DefaultParameterWidgetMapper : IParameterWidgetMapper
{
    public string GetWidget(AuthoredParameterDefinition definition)
    {
        if (!string.IsNullOrWhiteSpace(definition.Editor))
            return NormalizeWidget(definition.Editor!);

        if (definition.AllowedValues.Count > 0)
            return ParameterWidgets.Select;

        if (string.Equals(definition.Format, "date", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(definition.Format, "date-time", StringComparison.OrdinalIgnoreCase))
        {
            return ParameterWidgets.Date;
        }

        return definition.ValueKind switch
        {
            ParameterValueKind.Boolean => ParameterWidgets.Toggle,
            ParameterValueKind.Number or ParameterValueKind.Integer => ParameterWidgets.Number,
            ParameterValueKind.Object => ParameterWidgets.Object,
            ParameterValueKind.Array => ParameterWidgets.Collection,
            _ => ParameterWidgets.Text
        };
    }

    public IReadOnlyDictionary<string, string> BuildWidgetMap(AuthoredParameterSchema schema)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var property in schema.Properties)
            AddWidgets(property.Key, property, map);

        return map;
    }

    private void AddWidgets(string path, AuthoredParameterDefinition definition, IDictionary<string, string> map)
    {
        if (!string.IsNullOrWhiteSpace(path))
            map[path] = GetWidget(definition);

        foreach (var child in definition.Properties)
            AddWidgets($"{path}.{child.Key}", child, map);

        if (definition.Items is not null)
            AddWidgets($"{path}[]", definition.Items, map);
    }

    private static string NormalizeWidget(string widget) => widget.Trim().ToLowerInvariant() switch
    {
        "boolean" => ParameterWidgets.Toggle,
        "checkbox" => ParameterWidgets.Toggle,
        "datetime" => ParameterWidgets.Date,
        "integer" => ParameterWidgets.Number,
        "multiline" => ParameterWidgets.Textarea,
        _ => widget.Trim().ToLowerInvariant()
    };
}
