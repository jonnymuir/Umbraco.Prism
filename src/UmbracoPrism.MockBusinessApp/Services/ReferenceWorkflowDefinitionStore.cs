using Microsoft.Extensions.Logging;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.WorkflowRuntime.Abstractions;

namespace UmbracoPrism.MockBusinessApp.Services;

/// <summary>
/// In-memory runtime definition store seeded from the flattened reference workflow contract.
/// </summary>
public sealed class ReferenceWorkflowDefinitionStore : IWorkflowDefinitionStore
{
    public ReferenceWorkflowDefinitionStore()
    {
    }

    public ReferenceWorkflowDefinitionStore(object? _)
    {
    }

    public IReadOnlyDictionary<string, WorkflowDefinitionFile> LoadDefinitions(ILogger logger)
    {
        var definitions = new Dictionary<string, WorkflowDefinitionFile>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, definition) in ReferenceWorkflowRepository.GetReferenceWorkflows())
        {
            definitions[key] = definition;
            logger.LogInformation(
                "Loaded reference workflow '{Key}' as runtime lookup key for {DisplayName}",
                key,
                definition.DisplayName);
        }

        return definitions;
    }
}
