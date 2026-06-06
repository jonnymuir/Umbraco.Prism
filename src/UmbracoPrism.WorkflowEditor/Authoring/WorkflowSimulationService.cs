namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Lightweight authored-workflow simulator.
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

        for (var index = 0; index < requestedActions.Count && index < budget; index++)
        {
            var action = requestedActions[index];
            var stageRoute = GetStageRoutes(workflow, currentStage)
                .FirstOrDefault(route => string.Equals(route.Trigger, action, StringComparison.Ordinal));

            if (stageRoute is null)
            {
                return BuildResult(workflow, currentStage.StageKey, "transition-not-found", false, steps);
            }

            var resolution = ResolveFromGateway(stagesByKey, gatewaysByKey, stageRoute.Target, action);
            if (resolution.StopReason is not null)
            {
                return BuildResult(workflow, resolution.NodeKey, resolution.StopReason, false, steps);
            }

            if (!stagesByKey.TryGetValue(resolution.NodeKey, out var nextStage))
            {
                return BuildResult(workflow, resolution.NodeKey, "target-stage-missing", false, steps);
            }

            steps.Add(new WorkflowSimulationStep
            {
                FromStageKey = currentStage.StageKey,
                Action = action,
                ToStageKey = nextStage.StageKey,
                Condition = stageRoute.Condition?.Expression,
                RequiresRole = stageRoute.RequiresRole
            });

            currentStage = nextStage;
        }

        if (requestedActions.Count > budget)
        {
            return BuildResult(workflow, currentStage.StageKey, "max-steps-reached", false, steps);
        }

        var availableRoutes = GetStageRoutes(workflow, currentStage);
        var completed = availableRoutes.Count == 0;

        return BuildResult(
            workflow,
            currentStage.StageKey,
            completed ? "terminal-stage" : "awaiting-action",
            completed,
            steps);
    }

    private static WorkflowSimulationResult BuildResult(
        AuthoredWorkflow workflow,
        string currentStageKey,
        string stopReason,
        bool completed,
        IReadOnlyList<WorkflowSimulationStep> steps)
    {
        var currentStage = workflow.Stages.FirstOrDefault(stage =>
            string.Equals(stage.StageKey, currentStageKey, StringComparison.Ordinal));
        var availableTransitions = currentStage is null
            ? []
            : GetStageRoutes(workflow, currentStage).Select(route => new WorkflowSimulationTransitionOption
            {
                Action = route.Trigger,
                ToStageKey = route.Target,
                Condition = route.Condition?.Expression,
                RequiresRole = route.RequiresRole
            }).ToArray();

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

    private static IReadOnlyList<AuthoredRoute> GetStageRoutes(AuthoredWorkflow workflow, AuthoredStage stage)
    {
        if (stage.Routes.Count > 0)
        {
            return stage.Routes;
        }

        return workflow.Gateways
            .Where(gateway => string.Equals(gateway.Source, stage.StageKey, StringComparison.Ordinal))
            .SelectMany(gateway => gateway.Routes
                .Select(route => route.Trigger)
                .Distinct(StringComparer.Ordinal)
                .Select(trigger => new AuthoredRoute
                {
                    Id = $"{stage.StageKey}--{trigger}--{gateway.GatewayKey}",
                    Trigger = trigger,
                    Target = gateway.GatewayKey
                }))
            .OrderBy(route => route.Trigger, StringComparer.Ordinal)
            .ThenBy(route => route.Target, StringComparer.Ordinal)
            .ToArray();
    }

    private static (string NodeKey, string? StopReason) ResolveFromGateway(
        IReadOnlyDictionary<string, AuthoredStage> stagesByKey,
        IReadOnlyDictionary<string, AuthoredGateway> gatewaysByKey,
        string nodeKey,
        string action)
    {
        if (stagesByKey.ContainsKey(nodeKey))
        {
            return (nodeKey, null);
        }

        if (!gatewaysByKey.TryGetValue(nodeKey, out var gateway))
        {
            return (nodeKey, "target-stage-missing");
        }

        if (gateway.Kind == GatewayKind.Join)
        {
            return (nodeKey, "waiting-gateway");
        }

        var routes = gateway.Routes
            .Where(route => string.Equals(route.Trigger, action, StringComparison.Ordinal))
            .OrderBy(route => route.Target, StringComparer.Ordinal)
            .ThenBy(route => route.Id, StringComparer.Ordinal)
            .ToArray();

        if (routes.Length == 0)
        {
            routes = gateway.Routes
                .OrderBy(route => route.Trigger, StringComparer.Ordinal)
                .ThenBy(route => route.Target, StringComparer.Ordinal)
                .ThenBy(route => route.Id, StringComparer.Ordinal)
                .ToArray();
        }

        if (routes.Length == 0)
        {
            return (nodeKey, "target-stage-missing");
        }

        var nextTarget = routes[0].Target;
        return stagesByKey.ContainsKey(nextTarget)
            ? (nextTarget, null)
            : ResolveFromGateway(stagesByKey, gatewaysByKey, nextTarget, action);
    }
}
