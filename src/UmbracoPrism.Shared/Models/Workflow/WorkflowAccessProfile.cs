namespace UmbracoPrism.Shared.Models.Workflow;

public record WorkflowAccessProfile
{
    public static WorkflowAccessProfile UnrestrictedOwner { get; } = new()
    {
        UseLegacyLaneVisibility = true
    };

    public IReadOnlyList<string> VisibleQueues { get; init; } = [];

    public IReadOnlyList<string> StartableQueues { get; init; } = [];

    public IReadOnlyList<string> ActionableQueues { get; init; } = [];

    public bool RestrictToInstanceOwner { get; init; } = true;

    public bool UseLegacyLaneVisibility { get; init; }

    public bool CanViewQueue(string? queueName) => IsAllowed(queueName, VisibleQueues);

    public bool CanStartQueue(string? queueName) => IsAllowed(queueName, StartableQueues);

    public bool CanActInQueue(string? queueName) => IsAllowed(queueName, ActionableQueues);

    private static bool IsAllowed(string? queueName, IReadOnlyList<string> allowedQueues)
    {
        if (string.IsNullOrWhiteSpace(queueName))
        {
            return true;
        }

        if (allowedQueues.Count == 0)
        {
            return true;
        }

        return allowedQueues.Any(candidate =>
            string.Equals(candidate, queueName, StringComparison.OrdinalIgnoreCase));
    }
}
