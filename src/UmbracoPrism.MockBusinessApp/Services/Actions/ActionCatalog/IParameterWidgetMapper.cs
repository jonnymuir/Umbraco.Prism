namespace UmbracoPrism.MockBusinessApp.Services.Actions.ActionCatalog;

/// <summary>
/// Resolves authoring widgets for action-parameter definitions.
/// </summary>
public interface IParameterWidgetMapper
{
    string GetWidget(ActionParameterDefinition definition);

    IReadOnlyDictionary<string, string> BuildWidgetMap(ActionParameterSchema schema);
}
