namespace UmbracoPrism.MockBusinessApp.Services.Publishing;

using UmbracoPrism.ServiceBlueprintEditor.Authoring;


/// <summary>
/// Previews and publishes authored workflows into runtime-ready definitions.
/// </summary>
public interface IServiceBlueprintPublishService
{
    Task<PublishPreviewResult> PreviewAsync(AuthoredServiceBlueprint workflow, CancellationToken ct = default);

    Task<PublishResult> PublishAsync(AuthoredServiceBlueprint workflow, CancellationToken ct = default);
}
