using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.WorkflowRuntime.Services;

namespace UmbracoPrism.WorkflowRuntime.Api;

/// <summary>Request body for the simulate endpoint — bundles the two inputs a dry-run needs.</summary>
public sealed record SimulateWorkflowRequest(
    WorkflowDefinitionFile Workflow,
    IReadOnlyList<WorkflowRuntimeSimulationStep> Steps);
