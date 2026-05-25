namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Produces a <see cref="PreviewResult"/> by projecting the patched workflow and computing
/// a semantic diff and journey trace. No persistence, no side effects.
/// </summary>
public sealed class WorkflowPreviewService : IWorkflowPreviewService
{
    private readonly IWorkflowProjector _projector;

    public WorkflowPreviewService(IWorkflowProjector projector) => _projector = projector;

    /// <inheritdoc/>
    public PreviewResult Preview(AuthoredWorkflow original, AuthoredWorkflow patched)
    {
        var projResult = _projector.Project(patched);
        var diff       = ComputeDiff(original, patched);
        var journey    = ComputeJourneyTrace(patched);

        return new PreviewResult
        {
            ProjectedFile = projResult.File,
            Checksum      = projResult.Checksum,
            Diagnostics   = projResult.Diagnostics,
            Diff          = diff,
            JourneyTrace  = journey
        };
    }

    // ─── Semantic diff ───────────────────────────────────────────────────────

    private static IReadOnlyList<DiffEntry> ComputeDiff(AuthoredWorkflow original, AuthoredWorkflow patched)
    {
        var diff = new List<DiffEntry>();

        var origStages    = original.Stages.ToDictionary(s => s.StageKey, StringComparer.Ordinal);
        var patchedStages = patched.Stages.ToDictionary(s => s.StageKey, StringComparer.Ordinal);

        foreach (var key in patchedStages.Keys.Except(origStages.Keys, StringComparer.Ordinal))
            diff.Add(new StageAdded(key, patchedStages[key].DisplayName));

        foreach (var key in origStages.Keys.Except(patchedStages.Keys, StringComparer.Ordinal))
            diff.Add(new StageRemoved(key));

        foreach (var key in origStages.Keys.Intersect(patchedStages.Keys, StringComparer.Ordinal))
        {
            var fieldChanges = ComputeStageFieldChanges(origStages[key], patchedStages[key]);
            if (fieldChanges.Count > 0)
                diff.Add(new StageUpdated(key, fieldChanges));
        }

        // Handoffs
        var origHandoffs    = original.Handoffs.ToDictionary(h => h.Id, StringComparer.Ordinal);
        var patchedHandoffs = patched.Handoffs.ToDictionary(h => h.Id, StringComparer.Ordinal);

        foreach (var id in patchedHandoffs.Keys.Except(origHandoffs.Keys, StringComparer.Ordinal))
            diff.Add(new HandoffAdded(id, patchedHandoffs[id].Label));

        foreach (var id in origHandoffs.Keys.Except(patchedHandoffs.Keys, StringComparer.Ordinal))
            diff.Add(new HandoffRemoved(id));

        // Transitions — compare by composite key
        var origTrans    = original.Transitions.Select(TransitionKey).ToHashSet(StringComparer.Ordinal);
        var patchedTrans = patched.Transitions.Select(TransitionKey).ToHashSet(StringComparer.Ordinal);

        foreach (var tKey in origTrans.Except(patchedTrans).Union(patchedTrans.Except(origTrans)))
        {
            var parts = tKey.Split('\x00');
            if (parts.Length == 3)
                diff.Add(new TransitionChanged(parts[0], parts[1], parts[2]));
        }

        return diff;
    }

    private static string TransitionKey(AuthoredTransition t) =>
        $"{t.FromStage}\x00{t.ToStage}\x00{t.Action}";

    private static IReadOnlyList<string> ComputeStageFieldChanges(AuthoredStage original, AuthoredStage patched)
    {
        var changes = new List<string>();

        if (original.DisplayName != patched.DisplayName) changes.Add("displayName");
        if (original.Kind        != patched.Kind)        changes.Add("kind");
        if (original.Actor       != patched.Actor)       changes.Add("actor");
        if (original.LaneKey     != patched.LaneKey)     changes.Add("laneKey");

        var origFields    = original.Fields.ToDictionary(f => f.Key, StringComparer.Ordinal);
        var patchedFields = patched.Fields.ToDictionary(f => f.Key, StringComparer.Ordinal);

        foreach (var key in patchedFields.Keys.Except(origFields.Keys))
            changes.Add($"fields[{key}]:added");
        foreach (var key in origFields.Keys.Except(patchedFields.Keys))
            changes.Add($"fields[{key}]:removed");

        return changes;
    }

    // ─── Journey trace ───────────────────────────────────────────────────────

    /// <summary>
    /// Deterministic happy-path trace: starting from <see cref="AuthoredWorkflow.InitialStageKey"/>,
    /// always picks the first available transition (sorted by action name) until a terminal stage
    /// (one with no outgoing transitions) is reached or a cycle is detected.
    /// </summary>
    private static IReadOnlyList<string> ComputeJourneyTrace(AuthoredWorkflow workflow)
    {
        if (string.IsNullOrEmpty(workflow.InitialStageKey))
            return [];

        var trace   = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var current = workflow.InitialStageKey;

        while (!string.IsNullOrEmpty(current) && !visited.Contains(current))
        {
            trace.Add(current);
            visited.Add(current);

            current = workflow.Transitions
                .Where(t => t.FromStage == current)
                .OrderBy(t => t.Action, StringComparer.Ordinal)
                .Select(t => t.ToStage)
                .FirstOrDefault()!;
        }

        return trace;
    }
}
