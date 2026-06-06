using UmbracoPrism.Core.Models.Workflow;

namespace UmbracoPrism.Shared.Models.Workflow;

public record WorkflowQueueWorkListEnvelope
{
    public IReadOnlyList<WorkflowQueueWorkItem> Items { get; init; } = [];

    public DateTimeOffset ServerTimeUtc { get; init; } = DateTimeOffset.UtcNow;
}

public record WorkflowQueueWorkItem
{
    public string InstanceId { get; init; } = "";

    public string WorkflowKey { get; init; } = "";

    public string WorkflowDisplayName { get; init; } = "";

    public string StateKey { get; init; } = "";

    public string StateDisplayName { get; init; } = "";

    public string? QueueName { get; init; }

    public string TenantId { get; init; } = "";

    public string UserId { get; init; } = "";

    public int StateVersion { get; init; }

    public IReadOnlyList<WorkflowAction> AvailableActions { get; init; } = [];
}
