namespace UmbracoPrism.Shared.Models.ServiceDesign;

public record QueueWorkListEnvelope
{
    public IReadOnlyList<QueueWorkItem> Items { get; init; } = [];

    public DateTimeOffset ServerTimeUtc { get; init; } = DateTimeOffset.UtcNow;
}

public record QueueWorkItem
{
    public string InstanceId { get; init; } = "";

    public string BlueprintKey { get; init; } = "";

    public string BlueprintDisplayName { get; init; } = "";

    public string StageKey { get; init; } = "";

    public string StateDisplayName { get; init; } = "";

    public string? QueueName { get; init; }

    public string TenantId { get; init; } = "";

    public string UserId { get; init; } = "";

    public int StateVersion { get; init; }

    public IReadOnlyList<ServiceRequestAction> AvailableActions { get; init; } = [];
}
