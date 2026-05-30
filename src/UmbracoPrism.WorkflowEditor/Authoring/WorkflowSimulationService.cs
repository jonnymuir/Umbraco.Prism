namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Lightweight authored-workflow simulator used by the reference host and endpoint contract tests.
/// </summary>
public sealed class WorkflowSimulationService : IWorkflowSimulationService
{
    private const int DefaultMaxSteps = 20;
    private const int AbsoluteMaxSteps = 100;

    public WorkflowSimulationResult Simulate(
        AuthoredWorkflow workflow,
        IReadOnlyList<string>? actions = null,
        int? maxSteps = null)
    {
        var stagesByKey = workflow.Stages.ToDictionary(stage => stage.StageKey, StringComparer.Ordinal);
        var gatewaysByKey = workflow.Gateways.ToDictionary(gateway => gateway.GatewayKey, StringComparer.Ordinal);
        if (!stagesByKey.TryGetValue(workflow.InitialStageKey, out var initialStage))
        {
            return new WorkflowSimulationResult
            {
                InitialStageKey = workflow.InitialStageKey,
                CurrentStageKey = workflow.InitialStageKey,
                StopReason = "initial-stage-missing",
                Completed = false
            };
        }

        var requestedActions = actions ?? [];
        var budget = Math.Clamp(maxSteps ?? DefaultMaxSteps, 1, AbsoluteMaxSteps);
        var steps = new List<WorkflowSimulationStep>();
        var currentStage = initialStage;
        var stopReason = "awaiting-action";
        var completed = false;

        for (var i = 0; i < requestedActions.Count && i < budget; i++)
        {
            var action = requestedActions[i];
            var transition = workflow.Transitions.FirstOrDefault(
                candidate => string.Equals(candidate.Source, currentStage.StageKey, StringComparison.Ordinal)
                             && string.Equals(candidate.Trigger, action, StringComparison.Ordinal));

            if (transition is null)
            {
                stopReason = "transition-not-found";
                return BuildResult(workflow, currentStage.StageKey, stopReason, false, steps);
            }

            var resolution = ResolveNextStage(workflow, stagesByKey, gatewaysByKey, transition.Target);
            if (resolution.StopReason is not null)
            {
                return BuildResult(workflow, resolution.NodeKey, resolution.StopReason, false, steps);
            }

            if (!stagesByKey.TryGetValue(resolution.NodeKey, out var nextStage))
            {
                stopReason = "target-stage-missing";
                return BuildResult(workflow, resolution.NodeKey, stopReason, false, steps);
            }

            steps.Add(new WorkflowSimulationStep
            {
                FromStageKey = currentStage.StageKey,
                Action = transition.Trigger,
                ToStageKey = nextStage.StageKey,
                Condition = transition.Conditions.FirstOrDefault()?.Expression,
                RequiresRole = transition.RequiresRole
            });

            currentStage = nextStage;
        }

        if (requestedActions.Count > budget)
        {
            stopReason = "max-steps-reached";
            return BuildResult(workflow, currentStage.StageKey, stopReason, false, steps);
        }

        var availableTransitions = workflow.Transitions
            .Where(transition => string.Equals(transition.Source, currentStage.StageKey, StringComparison.Ordinal))
            .ToArray();

        if (availableTransitions.Length == 0)
        {
            stopReason = "terminal-stage";
            completed = true;
        }

        return BuildResult(workflow, currentStage.StageKey, stopReason, completed, steps);
    }

    private static WorkflowSimulationResult BuildResult(
        AuthoredWorkflow workflow,
        string currentStageKey,
        string stopReason,
        bool completed,
        IReadOnlyList<WorkflowSimulationStep> steps)
    {
        var availableTransitions = workflow.Transitions
            .Where(transition => string.Equals(transition.Source, currentStageKey, StringComparison.Ordinal))
            .Select(transition => new WorkflowSimulationTransitionOption
            {
                Action = transition.Trigger,
                ToStageKey = transition.Target,
                Condition = transition.Conditions.FirstOrDefault()?.Expression,
                RequiresRole = transition.RequiresRole
            })
            .ToArray();

        return new WorkflowSimulationResult
        {
            InitialStageKey = workflow.InitialStageKey,
            CurrentStageKey = currentStageKey,
            StopReason = stopReason,
            Completed = completed,
            Steps = steps,
            AvailableTransitions = availableTransitions
        };
    }

    private static (string NodeKey, string? StopReason) ResolveNextStage(
        AuthoredWorkflow workflow,
        IReadOnlyDictionary<string, AuthoredStage> stagesByKey,
        IReadOnlyDictionary<string, AuthoredGateway> gatewaysByKey,
        string nodeKey)
    {
        var current = nodeKey;
        var seenGateways = new HashSet<string>(StringComparer.Ordinal);

        while (!string.IsNullOrWhiteSpace(current))
        {
            if (stagesByKey.ContainsKey(current))
                return (current, null);

            if (!gatewaysByKey.TryGetValue(current, out var gateway))
                return (current, "target-stage-missing");

            if (!seenGateways.Add(current))
                return (current, "cycle-detected");

            if (gateway.Kind == GatewayKind.Join)
                return (current, "waiting-gateway");

            var nextTransition = workflow.Transitions
                .Where(transition => string.Equals(transition.Source, current, StringComparison.Ordinal))
                .OrderBy(transition => transition.Trigger, StringComparer.Ordinal)
                .ThenBy(transition => transition.Target, StringComparer.Ordinal)
                .FirstOrDefault();

            if (nextTransition is null)
                return (current, "target-stage-missing");

            current = nextTransition.Target;
        }

        return (nodeKey, "target-stage-missing");
    }
}
