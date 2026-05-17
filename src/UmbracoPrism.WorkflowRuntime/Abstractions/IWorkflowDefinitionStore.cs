using Microsoft.Extensions.Logging;
using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.WorkflowRuntime.Abstractions;

public interface IWorkflowDefinitionStore
{
    IReadOnlyDictionary<string, WorkflowDefinitionFile> LoadDefinitions(ILogger logger);
}
