namespace UmbracoPrism.ServiceBlueprintEditor.Authoring;

/// <summary>
/// Resolves authoring widgets for action-parameter definitions.
/// </summary>
public interface IParameterWidgetMapper
{
    string GetWidget(AuthoredParameterDefinition definition);

    IReadOnlyDictionary<string, string> BuildWidgetMap(AuthoredParameterSchema schema);
}
