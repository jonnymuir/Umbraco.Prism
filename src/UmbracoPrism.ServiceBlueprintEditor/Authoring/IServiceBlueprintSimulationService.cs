namespace UmbracoPrism.ServiceBlueprintEditor.Authoring;

/// <summary>
/// Provides deterministic, editor-side blueprint path simulation over the authored model.
/// </summary>
public interface IServiceBlueprintSimulationService
{
    ServiceBlueprintSimulationResult Simulate(
        AuthoredServiceBlueprint blueprint,
        IReadOnlyList<string>? actions = null,
        int? maxSteps = null);
}
