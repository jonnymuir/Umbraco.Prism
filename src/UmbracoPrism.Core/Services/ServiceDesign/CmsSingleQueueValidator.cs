using Wayfinder.Models.ServiceDesign;
using Wayfinder.Engine.Abstractions;

namespace UmbracoPrism.Core.Services.ServiceDesign;

/// <summary>
/// Enforces CMS Workflow's single-queue constraint (see <see cref="CmsQueue"/>) at
/// authoring time — the editor's own queue-picker already only ever offers the one queue, but
/// nothing stopped a definition saved by another route (a hand-edited seed file, the AI
/// authoring surface, a future importer) from declaring extra ones. Registered as an
/// <see cref="IServiceBlueprintStructuralValidator"/> so the shared <c>WorkflowRuntime</c> toolkit stays
/// completely unaware CMS Workflow — or this rule — exists.
/// </summary>
public sealed class CmsSingleQueueValidator : IServiceBlueprintStructuralValidator
{
    public IEnumerable<ServiceBlueprintDiagnostic> Validate(ServiceBlueprint workflow)
    {
        var queues = workflow.Queues ?? Array.Empty<QueueDefinition>();
        if (queues.Count == 1 && string.Equals(queues[0].Key, CmsQueue.Key, StringComparison.Ordinal))
        {
            yield break;
        }

        yield return new ServiceBlueprintDiagnostic(
            "CMS_WORKFLOW_SINGLE_QUEUE_ONLY",
            "queues",
            queues.Count == 0
                ? $"CMS Workflow definitions must declare exactly one queue, '{CmsQueue.Key}' — none were found."
                : $"CMS Workflow definitions must declare exactly one queue, '{CmsQueue.Key}' — found: " +
                  string.Join(", ", queues.Select(q => string.IsNullOrEmpty(q.Key) ? "(empty)" : q.Key)) + ".");
    }
}
