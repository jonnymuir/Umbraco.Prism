using System.Text.Json;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.WorkflowRuntime.Services;

namespace UmbracoPrism.WorkflowRuntime.Api;

/// <summary>Request body for the simulate endpoint — bundles the inputs a dry-run needs.</summary>
/// <param name="MockServiceInputs">
/// Mock values for any <c>source: "service"</c> calculation field, e.g.
/// <c>{ "member": { "age": 47 } }</c>. Omit if the definition has none.
/// </param>
public sealed record SimulateWorkflowRequest(
    WorkflowDefinitionFile Workflow,
    IReadOnlyList<WorkflowRuntimeSimulationStep> Steps,
    JsonElement? MockServiceInputs = null);
