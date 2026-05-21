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
                candidate => string.Equals(candidate.FromStage, currentStage.StageKey, StringComparison.Ordinal)
                             && string.Equals(candidate.Action, action, StringComparison.Ordinal));

            if (transition is null)
            {
                stopReason = "transition-not-found";
                return BuildResult(workflow, currentStage.StageKey, stopReason, false, steps);
            }

            if (!stagesByKey.TryGetValue(transition.ToStage, out var nextStage))
            {
                stopReason = "target-stage-missing";
                return BuildResult(workflow, currentStage.StageKey, stopReason, false, steps);
            }

            steps.Add(new WorkflowSimulationStep
            {
                FromStageKey = currentStage.StageKey,
                Action = transition.Action,
                ToStageKey = transition.ToStage,
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
            .Where(transition => string.Equals(transition.FromStage, currentStage.StageKey, StringComparison.Ordinal))
            .ToArray();

        if (availableTransitions.Length == 0)
        {
            stopReason = "terminal-stage";
            completed = true;
        }
        else if (currentStage.Kind == StageKind.Waiting || currentStage.Kind == StageKind.StatusTimeline)
        {
            stopReason = "waiting-stage";
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
            .Where(transition => string.Equals(transition.FromStage, currentStageKey, StringComparison.Ordinal))
            .Select(transition => new WorkflowSimulationTransitionOption
            {
                Action = transition.Action,
                ToStageKey = transition.ToStage,
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
}
