using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.WorkflowRuntime.Abstractions;

namespace UmbracoPrism.Core.Services.Workflow;

/// <summary>
/// Enforces CMS Workflow's single-queue constraint (see <see cref="CmsWorkflowQueue"/>) at
/// authoring time — the editor's own queue-picker already only ever offers the one queue, but
/// nothing stopped a definition saved by another route (a hand-edited seed file, the AI
/// authoring surface, a future importer) from declaring extra ones. Registered as an
/// <see cref="IWorkflowStructuralValidator"/> so the shared <c>WorkflowRuntime</c> toolkit stays
/// completely unaware CMS Workflow — or this rule — exists.
/// </summary>
public sealed class CmsWorkflowSingleQueueValidator : IWorkflowStructuralValidator
{
    public IEnumerable<WorkflowDiagnostic> Validate(WorkflowDefinitionFile workflow)
    {
        var queues = workflow.Queues ?? Array.Empty<WorkflowQueueDefinition>();
        if (queues.Count == 1 && string.Equals(queues[0].Key, CmsWorkflowQueue.Key, StringComparison.Ordinal))
        {
            yield break;
        }

        yield return new WorkflowDiagnostic(
            "CMS_WORKFLOW_SINGLE_QUEUE_ONLY",
            "queues",
            queues.Count == 0
                ? $"CMS Workflow definitions must declare exactly one queue, '{CmsWorkflowQueue.Key}' — none were found."
                : $"CMS Workflow definitions must declare exactly one queue, '{CmsWorkflowQueue.Key}' — found: " +
                  string.Join(", ", queues.Select(q => string.IsNullOrEmpty(q.Key) ? "(empty)" : q.Key)) + ".");
    }
}
