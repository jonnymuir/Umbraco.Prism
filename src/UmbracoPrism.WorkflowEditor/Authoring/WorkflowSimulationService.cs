namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Lightweight authored-workflow simulator. Walks the graph by:
///   stage → that stage's owning gateway (if any) → matched route → resolve target (stage or gateway).
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
        var gatewayBySourceStage = workflow.Gateways
            .Where(g => !string.IsNullOrWhiteSpace(g.Source))
            .GroupBy(g => g.Source, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

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
            if (!gatewayBySourceStage.TryGetValue(currentStage.StageKey, out var owningGateway))
            {
                stopReason = "transition-not-found";
                return BuildResult(workflow, gatewayBySourceStage, currentStage.StageKey, stopReason, false, steps);
            }

            var route = owningGateway.Routes.FirstOrDefault(
                r => string.Equals(r.Trigger, action, StringComparison.Ordinal));

            if (route is null)
            {
                stopReason = "transition-not-found";
                return BuildResult(workflow, gatewayBySourceStage, currentStage.StageKey, stopReason, false, steps);
            }

            var resolution = ResolveNextStage(stagesByKey, gatewaysByKey, route.Target);
            if (resolution.StopReason is not null)
            {
                return BuildResult(workflow, gatewayBySourceStage, resolution.NodeKey, resolution.StopReason, false, steps);
            }

            if (!stagesByKey.TryGetValue(resolution.NodeKey, out var nextStage))
            {
                stopReason = "target-stage-missing";
                return BuildResult(workflow, gatewayBySourceStage, resolution.NodeKey, stopReason, false, steps);
            }

            steps.Add(new WorkflowSimulationStep
            {
                FromStageKey = currentStage.StageKey,
                Action = route.Trigger,
                ToStageKey = nextStage.StageKey,
                Condition = route.Condition?.Expression,
                RequiresRole = route.RequiresRole
            });

            currentStage = nextStage;
        }

        if (requestedActions.Count > budget)
        {
            stopReason = "max-steps-reached";
            return BuildResult(workflow, gatewayBySourceStage, currentStage.StageKey, stopReason, false, steps);
        }

        if (!gatewayBySourceStage.TryGetValue(currentStage.StageKey, out var outgoingGateway)
            || outgoingGateway.Routes.Count == 0)
        {
            stopReason = "terminal-stage";
            completed = true;
        }

        return BuildResult(workflow, gatewayBySourceStage, currentStage.StageKey, stopReason, completed, steps);
    }

    private static WorkflowSimulationResult BuildResult(
        AuthoredWorkflow workflow,
        IReadOnlyDictionary<string, AuthoredGateway> gatewayBySourceStage,
        string currentStageKey,
        string stopReason,
        bool completed,
        IReadOnlyList<WorkflowSimulationStep> steps)
    {
        var availableTransitions = gatewayBySourceStage.TryGetValue(currentStageKey, out var owning)
            ? owning.Routes.Select(route => new WorkflowSimulationTransitionOption
            {
                Action = route.Trigger,
                ToStageKey = route.Target,
                Condition = route.Condition?.Expression,
                RequiresRole = route.RequiresRole
            }).ToArray()
            : [];

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

            // For split gateways routed-to from elsewhere (chained gateways), follow the
            // canonically-first outgoing route.
            var nextRoute = gateway.Routes
                .OrderBy(r => r.Trigger, StringComparer.Ordinal)
                .ThenBy(r => r.Target, StringComparer.Ordinal)
                .FirstOrDefault();

            if (nextRoute is null)
                return (current, "target-stage-missing");

            current = nextRoute.Target;
        }

        return (nodeKey, "target-stage-missing");
    }
}
