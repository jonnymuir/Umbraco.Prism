using Microsoft.Extensions.Logging;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.WorkflowRuntime.Abstractions;

namespace UmbracoPrism.WorkflowRuntime.Stores;

/// <summary>
/// Serves a single, caller-supplied definition. Used to wire a real <c>WorkflowRuntimeEngine</c>
/// against one workflow at a time — e.g. for dry-run simulation of a definition that isn't
/// (yet) persisted anywhere.
/// </summary>
public sealed class SingleDefinitionWorkflowStore(WorkflowDefinitionFile definition) : IWorkflowDefinitionStore
{
    public IReadOnlyDictionary<string, WorkflowDefinitionFile> LoadDefinitions(ILogger logger) =>
        new Dictionary<string, WorkflowDefinitionFile>(StringComparer.OrdinalIgnoreCase)
        {
            [definition.DefinitionKey] = definition
        };
}
