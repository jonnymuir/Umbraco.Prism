using Microsoft.Extensions.Logging;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.WorkflowEditor.Authoring;
using UmbracoPrism.WorkflowRuntime.Abstractions;

namespace UmbracoPrism.MockBusinessApp.Services;

/// <summary>
/// In-memory workflow definition store seeded from the reference repository.
/// Provides exactly 4 demo workflows to the runtime engine.
/// </summary>
public sealed class ReferenceWorkflowDefinitionStore(IWorkflowProjector projector) : IWorkflowDefinitionStore
{
    public IReadOnlyDictionary<string, WorkflowDefinitionFile> LoadDefinitions(ILogger logger)
    {
        var definitions = new Dictionary<string, WorkflowDefinitionFile>(StringComparer.OrdinalIgnoreCase);
        var referenceWorkflows = ReferenceWorkflowRepository.GetReferenceWorkflows();

        foreach (var (key, authored) in referenceWorkflows)
        {
            try
            {
                var projectResult = projector.Project(authored);
                if (projectResult.HasErrors)
                {
                    logger.LogError(
                        "Failed to project reference workflow {Key}: {Errors}",
                        key,
                        string.Join("; ", projectResult.Diagnostics
                            .Where(d => d.Severity == DiagnosticSeverity.Error)
                            .Select(d => d.Message)));
                    continue;
                }

                if (projectResult.File is not null)
                {
                    definitions[key] = projectResult.File;
                    logger.LogInformation(
                        "Loaded reference workflow '{Key}' as runtime lookup key for {DisplayName}",
                        key,
                        projectResult.File.DisplayName);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load reference workflow {Key}", key);
            }
        }

        return definitions;
    }
}
