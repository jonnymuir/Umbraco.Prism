namespace UmbracoPrism.ServiceBlueprintEditor.Authoring;

/// <summary>
/// Lightweight authored-workflow simulator.
/// </summary>
public sealed class ServiceBlueprintSimulationService : IServiceBlueprintSimulationService
{
    private const int DefaultMaxSteps = 20;
    private const int AbsoluteMaxSteps = 100;

    public ServiceBlueprintSimulationResult Simulate(
        AuthoredServiceBlueprint blueprint,
        IReadOnlyList<string>? actions = null,
        int? maxSteps = null)
    {
        var touchpointsByKey = blueprint.Touchpoints.ToDictionary(touchpoint => touchpoint.TouchpointKey, StringComparer.Ordinal);
        var gatewaysByKey = blueprint.Gateways.ToDictionary(gateway => gateway.GatewayKey, StringComparer.Ordinal);

        if (!touchpointsByKey.TryGetValue(blueprint.InitialTouchpointKey, out var initialTouchpoint))
        {
            return new ServiceBlueprintSimulationResult
            {
                InitialTouchpointKey = blueprint.InitialTouchpointKey,
                CurrentTouchpointKey = blueprint.InitialTouchpointKey,
                StopReason = "initial-touchpoint-missing",
                Completed = false
            };
        }

        var requestedActions = actions ?? [];
        var budget = Math.Clamp(maxSteps ?? DefaultMaxSteps, 1, AbsoluteMaxSteps);
        var steps = new List<ServiceBlueprintSimulationStep>();
        var currentTouchpoint = initialTouchpoint;

        for (var index = 0; index < requestedActions.Count && index < budget; index++)
        {
            var action = requestedActions[index];
            var touchpointRoute = GetTouchpointRoutes(blueprint, currentTouchpoint)
                .FirstOrDefault(route => string.Equals(route.Trigger, action, StringComparison.Ordinal));

            if (touchpointRoute is null)
            {
                return BuildResult(blueprint, currentTouchpoint.TouchpointKey, "transition-not-found", false, steps);
            }

            var resolution = ResolveFromGateway(touchpointsByKey, gatewaysByKey, touchpointRoute.Target, action);
            if (resolution.StopReason is not null)
            {
                return BuildResult(blueprint, resolution.NodeKey, resolution.StopReason, false, steps);
            }

            if (!touchpointsByKey.TryGetValue(resolution.NodeKey, out var nextTouchpoint))
            {
                return BuildResult(blueprint, resolution.NodeKey, "target-touchpoint-missing", false, steps);
            }

            steps.Add(new ServiceBlueprintSimulationStep
            {
                FromTouchpointKey = currentTouchpoint.TouchpointKey,
                Action = action,
                ToTouchpointKey = nextTouchpoint.TouchpointKey,
                Condition = touchpointRoute.Condition?.Expression,
                RequiresRole = touchpointRoute.RequiresRole
            });

            currentTouchpoint = nextTouchpoint;
        }

        if (requestedActions.Count > budget)
        {
            return BuildResult(blueprint, currentTouchpoint.TouchpointKey, "max-steps-reached", false, steps);
        }

        var availableRoutes = GetTouchpointRoutes(blueprint, currentTouchpoint);
        var completed = availableRoutes.Count == 0;

        return BuildResult(
            blueprint,
            currentTouchpoint.TouchpointKey,
            completed ? "terminal-touchpoint" : "awaiting-action",
            completed,
            steps);
    }

    private static ServiceBlueprintSimulationResult BuildResult(
        AuthoredServiceBlueprint blueprint,
        string currentTouchpointKey,
        string stopReason,
        bool completed,
        IReadOnlyList<ServiceBlueprintSimulationStep> steps)
    {
        var currentTouchpoint = blueprint.Touchpoints.FirstOrDefault(touchpoint =>
            string.Equals(touchpoint.TouchpointKey, currentTouchpointKey, StringComparison.Ordinal));
        var availableTransitions = currentTouchpoint is null
            ? []
            : GetTouchpointRoutes(blueprint, currentTouchpoint).Select(route => new ServiceBlueprintSimulationRouteOption
            {
                Action = route.Trigger,
                ToTouchpointKey = route.Target,
                Condition = route.Condition?.Expression,
                RequiresRole = route.RequiresRole
            }).ToArray();

        return new ServiceBlueprintSimulationResult
        {
            InitialTouchpointKey = blueprint.InitialTouchpointKey,
            CurrentTouchpointKey = currentTouchpointKey,
            StopReason = stopReason,
            Completed = completed,
            Steps = steps,
            AvailableTransitions = availableTransitions
        };
    }

    private static IReadOnlyList<AuthoredRoute> GetTouchpointRoutes(AuthoredServiceBlueprint blueprint, AuthoredTouchpoint touchpoint)
    {
        if (touchpoint.Routes.Count > 0)
        {
            return touchpoint.Routes;
        }

        return blueprint.Gateways
            .Where(gateway => string.Equals(gateway.Source, touchpoint.TouchpointKey, StringComparison.Ordinal))
            .SelectMany(gateway => gateway.Routes
                .Select(route => route.Trigger)
                .Distinct(StringComparer.Ordinal)
                .Select(trigger => new AuthoredRoute
                {
                    Id = $"{touchpoint.TouchpointKey}--{trigger}--{gateway.GatewayKey}",
                    Trigger = trigger,
                    Target = gateway.GatewayKey
                }))
            .OrderBy(route => route.Trigger, StringComparer.Ordinal)
            .ThenBy(route => route.Target, StringComparer.Ordinal)
            .ToArray();
    }

    private static (string NodeKey, string? StopReason) ResolveFromGateway(
        IReadOnlyDictionary<string, AuthoredTouchpoint> touchpointsByKey,
        IReadOnlyDictionary<string, AuthoredGateway> gatewaysByKey,
        string nodeKey,
        string action)
    {
        if (touchpointsByKey.ContainsKey(nodeKey))
        {
            return (nodeKey, null);
        }

        if (!gatewaysByKey.TryGetValue(nodeKey, out var gateway))
        {
            return (nodeKey, "target-touchpoint-missing");
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
            return (nodeKey, "target-touchpoint-missing");
        }

        var nextTarget = routes[0].Target;
        return touchpointsByKey.ContainsKey(nextTarget)
            ? (nextTarget, null)
            : ResolveFromGateway(touchpointsByKey, gatewaysByKey, nextTarget, action);
    }
}
