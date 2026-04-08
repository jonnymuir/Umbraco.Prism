using UmbracoPrism.Core.Models.Workflow;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Static factory for creating consistent workflow response envelopes.
/// </summary>
public static class WorkflowResponseFactory
{
    /// <summary>
    /// Creates an ask_now response with render payload.
    /// </summary>
    public static WorkflowResponseEnvelope AskNow(
        WorkflowRenderPayload renderPayload,
        string instanceId,
        int stateVersion,
        string correlationId)
    {
        return new WorkflowResponseEnvelope
        {
            InstanceId = instanceId,
            ResponseState = "ask_now",
            StateVersion = stateVersion,
            CorrelationId = correlationId,
            ServerTimeUtc = DateTimeOffset.UtcNow,
            PollAfterMs = null,
            Render = renderPayload,
            Problems = Array.Empty<WorkflowProblem>()
        };
    }

    /// <summary>
    /// Creates a wait response with poll interval.
    /// </summary>
    public static WorkflowResponseEnvelope Wait(
        int pollAfterMs,
        string instanceId,
        int stateVersion,
        string correlationId)
    {
        return new WorkflowResponseEnvelope
        {
            InstanceId = instanceId,
            ResponseState = "wait",
            StateVersion = stateVersion,
            CorrelationId = correlationId,
            ServerTimeUtc = DateTimeOffset.UtcNow,
            PollAfterMs = pollAfterMs,
            Render = null,
            Problems = Array.Empty<WorkflowProblem>()
        };
    }

    /// <summary>
    /// Creates a complete response with outcome.
    /// </summary>
    public static WorkflowResponseEnvelope Complete(
        string outcomeKey,
        string instanceId,
        int stateVersion,
        string correlationId)
    {
        var completionPayload = new WorkflowRenderPayload
        {
            Archetype = "Completion",
            StateDisplayName = outcomeKey,
            FieldGroups = Array.Empty<FieldGroupRenderPayload>(),
            AvailableActions = Array.Empty<WorkflowAction>()
        };

        return new WorkflowResponseEnvelope
        {
            InstanceId = instanceId,
            ResponseState = "complete",
            StateVersion = stateVersion,
            CorrelationId = correlationId,
            ServerTimeUtc = DateTimeOffset.UtcNow,
            PollAfterMs = null,
            Render = completionPayload,
            Problems = Array.Empty<WorkflowProblem>()
        };
    }

    /// <summary>
    /// Creates an error response with problems list.
    /// </summary>
    public static WorkflowResponseEnvelope Error(
        IReadOnlyList<WorkflowProblem> problems,
        string? instanceId,
        string correlationId)
    {
        return new WorkflowResponseEnvelope
        {
            InstanceId = instanceId ?? string.Empty,
            ResponseState = "error",
            StateVersion = 0,
            CorrelationId = correlationId,
            ServerTimeUtc = DateTimeOffset.UtcNow,
            PollAfterMs = null,
            Render = null,
            Problems = problems
        };
    }
}
