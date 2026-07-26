namespace UmbracoPrism.ServiceBlueprintEditor.Authoring;

/// <summary>
/// Compiles an <see cref="AuthoredServiceBlueprint"/> into a runtime <see cref="UmbracoPrism.Shared.Models.ServiceDesign.ServiceBlueprint"/>.
/// Implementations must be deterministic: identical input always produces byte-identical output.
/// </summary>
public interface IServiceBlueprintProjector
{
    /// <summary>
    /// Projects the authored blueprint through the five-touchpoint pipeline:
    /// validate → normalise → emit → checksum.
    /// </summary>
    /// <param name="authored">The authored source blueprint.</param>
    /// <returns>A <see cref="ProjectionResult"/> containing the runtime file and diagnostics.</returns>
    ProjectionResult Project(AuthoredServiceBlueprint authored);
}
