namespace UmbracoPrism.ServiceBlueprintEditor.Authoring;

/// <summary>
/// Result of simulating a path through an authored blueprint.
/// </summary>
public record ServiceBlueprintSimulationResult
{
    public required string InitialTouchpointKey { get; init; }

    public required string CurrentTouchpointKey { get; init; }

    public required string StopReason { get; init; }

    public bool Completed { get; init; }

    public IReadOnlyList<ServiceBlueprintSimulationStep> Steps { get; init; } = [];

    public IReadOnlyList<ServiceBlueprintSimulationRouteOption> AvailableTransitions { get; init; } = [];
}

/// <summary>
/// One executed hop in a simulated path.
/// </summary>
public record ServiceBlueprintSimulationStep
{
    public required string FromTouchpointKey { get; init; }

    public required string Action { get; init; }

    public required string ToTouchpointKey { get; init; }

    public string? Condition { get; init; }

    public string? RequiresRole { get; init; }
}

/// <summary>
/// A transition the simulation could take next from the current touchpoint.
/// </summary>
public record ServiceBlueprintSimulationRouteOption
{
    public required string Action { get; init; }

    public required string ToTouchpointKey { get; init; }

    public string? Condition { get; init; }

    public string? RequiresRole { get; init; }
}
