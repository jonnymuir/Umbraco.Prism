using System.Text.Json;

namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Applies a <see cref="ProposalEnvelope"/> to an <see cref="AuthoredWorkflow"/> immutably.
/// Each op is applied in sequence; the first error aborts the chain and returns the original.
/// The final result is validated through <see cref="IWorkflowProjector"/> before being returned.
/// </summary>
public sealed class WorkflowPatchService : IWorkflowPatchService
{
    // Lenient read options for deserializing op values from the JSON envelope.
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
        // Enum converters are declared directly on StageKind via [JsonConverter].
    };

    private readonly IWorkflowProjector _projector;

    public WorkflowPatchService(IWorkflowProjector projector) => _projector = projector;

    /// <inheritdoc/>
    public PatchResult Apply(ProposalEnvelope envelope, AuthoredWorkflow original)
    {
        var diagnostics = new List<ProjectionDiagnostic>();
        var current = original;

        foreach (var op in envelope.Ops)
        {
            var (next, opDiags) = ApplyOp(op, current, envelope.Placement);
            diagnostics.AddRange(opDiags);

            if (opDiags.Any(d => d.Severity == DiagnosticSeverity.Error))
                return new PatchResult { Updated = original, Diagnostics = diagnostics };

            current = next;
        }

        // Bump version on a successful patch sequence.
        current = current with { Version = original.Version + 1 };

        // Validate the output through the projector — errors mean we revert to original.
        var projResult = _projector.Project(current);
        if (projResult.HasErrors)
        {
            diagnostics.AddRange(projResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            return new PatchResult { Updated = original, Diagnostics = diagnostics };
        }

        // Carry forward non-error diagnostics from the projector (e.g. warnings).
        diagnostics.AddRange(projResult.Diagnostics.Where(d => d.Severity != DiagnosticSeverity.Error));
        return new PatchResult { Updated = current, Diagnostics = diagnostics };
    }

    // ─── Op dispatch ─────────────────────────────────────────────────────────

    private static (AuthoredWorkflow, List<ProjectionDiagnostic>) ApplyOp(
        PatchOp op, AuthoredWorkflow current, PatchPlacement? placement) =>
        op.Op switch
        {
            "insert-stage"   => ApplyInsertStage(op, current, placement),
            "remove-stage"   => ApplyRemoveStage(op, current),
            "update-stage"   => ApplyUpdateStage(op, current),
            "insert-handoff" => ApplyInsertHandoff(op, current),
            "add-route"      => ApplyAddRoute(op, current),
            "update-route"   => ApplyUpdateRoute(op, current),
            "delete-route"   => ApplyDeleteRoute(op, current),
            _ => (current, [Err("PATCH001", $"Unknown op '{op.Op}'.", null)])
        };

    // ─── insert-stage ────────────────────────────────────────────────────────

    private static (AuthoredWorkflow, List<ProjectionDiagnostic>) ApplyInsertStage(
        PatchOp op, AuthoredWorkflow current, PatchPlacement? placement)
    {
        if (op.Value is null)
            return (current, [Err("PATCH002", "insert-stage requires a value.", null)]);

        AuthoredStage? stage = TryDeserialize<AuthoredStage>(op.Value.Value);
        if (stage is null)
            return (current, [Err("PATCH002", "insert-stage value is not a valid AuthoredStage.", null)]);

        if (string.IsNullOrWhiteSpace(stage.StageKey))
            return (current, [Err("PATCH003", "insert-stage value must have a non-empty StageKey.", null)]);

        var stages = current.Stages.ToList();
        int insertAt = stages.Count; // default: append

        // op-level before/after take precedence over envelope-level placement
        var before = op.Before ?? placement?.InsertBeforeStageKey;
        var after  = op.After  ?? placement?.InsertAfterStageKey;

        if (before != null)
        {
            var idx = stages.FindIndex(s => s.StageKey == before);
            if (idx < 0)
                return (current, [Err("PATCH004", $"insert-stage: 'before' stage '{before}' not found.", null)]);
            insertAt = idx;
        }
        else if (after != null)
        {
            var idx = stages.FindIndex(s => s.StageKey == after);
            if (idx < 0)
                return (current, [Err("PATCH004", $"insert-stage: 'after' stage '{after}' not found.", null)]);
            insertAt = idx + 1;
        }

        stages.Insert(insertAt, stage);
        return (current with { Stages = stages }, []);
    }

    // ─── remove-stage ────────────────────────────────────────────────────────

    private static (AuthoredWorkflow, List<ProjectionDiagnostic>) ApplyRemoveStage(
        PatchOp op, AuthoredWorkflow current)
    {
        var stageKey = ResolveStageKey(op, current);
        if (stageKey is null)
            return (current, [Err("PATCH005", "remove-stage: cannot determine target stage from path or value.", null)]);

        var stages = current.Stages.ToList();
        var removed = stages.RemoveAll(s => s.StageKey == stageKey);
        if (removed == 0)
            return (current, [Err("PATCH006", $"remove-stage: stage '{stageKey}' not found.", stageKey)]);

        return (current with { Stages = stages }, []);
    }

    // ─── update-stage ────────────────────────────────────────────────────────

    private static (AuthoredWorkflow, List<ProjectionDiagnostic>) ApplyUpdateStage(
        PatchOp op, AuthoredWorkflow current)
    {
        if (op.Value is null)
            return (current, [Err("PATCH002", "update-stage requires a value.", null)]);

        AuthoredStage? updated = TryDeserialize<AuthoredStage>(op.Value.Value);
        if (updated is null)
            return (current, [Err("PATCH002", "update-stage value is not a valid AuthoredStage.", null)]);

        // Prefer path-derived key; fall back to the key on the value itself.
        var stageKey = ResolveStageKey(op, current) ?? updated.StageKey;
        var stages = current.Stages.ToList();
        var idx = stages.FindIndex(s => s.StageKey == stageKey);
        if (idx < 0)
            return (current, [Err("PATCH006", $"update-stage: stage '{stageKey}' not found.", stageKey)]);

        stages[idx] = updated;
        return (current with { Stages = stages }, []);
    }

    // ─── insert-handoff ──────────────────────────────────────────────────────

    private static (AuthoredWorkflow, List<ProjectionDiagnostic>) ApplyInsertHandoff(
        PatchOp op, AuthoredWorkflow current)
    {
        if (op.Value is null)
            return (current, [Err("PATCH002", "insert-handoff requires a value.", null)]);

        AuthoredHandoff? handoff = TryDeserialize<AuthoredHandoff>(op.Value.Value);
        if (handoff is null)
            return (current, [Err("PATCH002", "insert-handoff value is not a valid AuthoredHandoff.", null)]);

        if (string.IsNullOrWhiteSpace(handoff.Id))
            return (current, [Err("PATCH003", "insert-handoff value must have a non-empty Id.", null)]);

        var handoffs = current.Handoffs.ToList();
        handoffs.Add(handoff);
        return (current with { Handoffs = handoffs }, []);
    }

    // ─── add-route / update-route / delete-route ─────────────────────────────

    private static (AuthoredWorkflow, List<ProjectionDiagnostic>) ApplyAddRoute(
        PatchOp op, AuthoredWorkflow current)
    {
        if (op.Value is null)
            return (current, [Err("PATCH002", "add-route requires a value.", null)]);

        var gatewayKey = ReadGatewayKey(op);
        if (string.IsNullOrWhiteSpace(gatewayKey))
            return (current, [Err("PATCH010", "add-route requires a gatewayKey in path or value.", null)]);

        var route = TryDeserialize<AuthoredRoute>(op.Value.Value);
        if (route is null)
            return (current, [Err("PATCH002", "add-route value is not a valid AuthoredRoute.", null)]);

        if (string.IsNullOrWhiteSpace(route.Id))
            return (current, [Err("PATCH011", "add-route value must have a non-empty Id.", null)]);

        var gateways = current.Gateways.ToList();
        var idx = gateways.FindIndex(g => g.GatewayKey == gatewayKey);
        if (idx < 0)
            return (current, [Err("PATCH012", $"add-route: gateway '{gatewayKey}' not found.", null)]);

        var updatedGateway = gateways[idx] with { Routes = [.. gateways[idx].Routes, route] };
        gateways[idx] = updatedGateway;
        return (current with { Gateways = gateways }, []);
    }

    private static (AuthoredWorkflow, List<ProjectionDiagnostic>) ApplyUpdateRoute(
        PatchOp op, AuthoredWorkflow current)
    {
        if (op.Value is null)
            return (current, [Err("PATCH002", "update-route requires a value.", null)]);

        var gatewayKey = ReadGatewayKey(op);
        if (string.IsNullOrWhiteSpace(gatewayKey))
            return (current, [Err("PATCH010", "update-route requires a gatewayKey in path or value.", null)]);

        var route = TryDeserialize<AuthoredRoute>(op.Value.Value);
        if (route is null)
            return (current, [Err("PATCH002", "update-route value is not a valid AuthoredRoute.", null)]);

        if (string.IsNullOrWhiteSpace(route.Id))
            return (current, [Err("PATCH011", "update-route value must have a non-empty Id.", null)]);

        var gateways = current.Gateways.ToList();
        var gIdx = gateways.FindIndex(g => g.GatewayKey == gatewayKey);
        if (gIdx < 0)
            return (current, [Err("PATCH012", $"update-route: gateway '{gatewayKey}' not found.", null)]);

        var routes = gateways[gIdx].Routes.ToList();
        var rIdx = routes.FindIndex(r => r.Id == route.Id);
        if (rIdx < 0)
            routes.Add(route);
        else
            routes[rIdx] = route;

        gateways[gIdx] = gateways[gIdx] with { Routes = routes };
        return (current with { Gateways = gateways }, []);
    }

    private static (AuthoredWorkflow, List<ProjectionDiagnostic>) ApplyDeleteRoute(
        PatchOp op, AuthoredWorkflow current)
    {
        var gatewayKey = ReadGatewayKey(op);
        var routeId = ReadRouteId(op);
        if (string.IsNullOrWhiteSpace(gatewayKey) || string.IsNullOrWhiteSpace(routeId))
            return (current, [Err("PATCH010", "delete-route requires a gatewayKey and routeId in path or value.", null)]);

        var gateways = current.Gateways.ToList();
        var gIdx = gateways.FindIndex(g => g.GatewayKey == gatewayKey);
        if (gIdx < 0)
            return (current, [Err("PATCH012", $"delete-route: gateway '{gatewayKey}' not found.", null)]);

        var routes = gateways[gIdx].Routes.ToList();
        var removed = routes.RemoveAll(r => r.Id == routeId);
        if (removed == 0)
            return (current, [Err("PATCH013", $"delete-route: route '{routeId}' not found on gateway '{gatewayKey}'.", null)]);

        gateways[gIdx] = gateways[gIdx] with { Routes = routes };
        return (current with { Gateways = gateways }, []);
    }

    private static string? ReadGatewayKey(PatchOp op)
    {
        // Prefer JSON-Pointer path like /gateways/{key}/routes[/{id}]
        if (!string.IsNullOrEmpty(op.Path))
        {
            var parts = op.Path.TrimStart('/').Split('/');
            if (parts.Length >= 2 && parts[0].Equals("gateways", StringComparison.OrdinalIgnoreCase))
                return parts[1];
        }

        // Fall back to a top-level "gatewayKey" property on the op value.
        if (op.Value is { } value && value.ValueKind == JsonValueKind.Object &&
            value.TryGetProperty("gatewayKey", out var gk) && gk.ValueKind == JsonValueKind.String)
        {
            return gk.GetString();
        }

        return null;
    }

    private static string? ReadRouteId(PatchOp op)
    {
        if (!string.IsNullOrEmpty(op.Path))
        {
            var parts = op.Path.TrimStart('/').Split('/');
            if (parts.Length >= 4 && parts[0].Equals("gateways", StringComparison.OrdinalIgnoreCase)
                && parts[2].Equals("routes", StringComparison.OrdinalIgnoreCase))
                return parts[3];
        }

        if (op.Value is { } value && value.ValueKind == JsonValueKind.Object)
        {
            if (value.TryGetProperty("routeId", out var rid) && rid.ValueKind == JsonValueKind.String)
                return rid.GetString();
            if (value.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                return id.GetString();
        }

        return null;
    }

    // ─── Path / key resolution ───────────────────────────────────────────────

    /// <summary>
    /// Resolves a stage key from a JSON-Pointer path (e.g. <c>/stages/declaration</c> or <c>/stages/2</c>)
    /// or falls back to the stage key embedded in the op value.
    /// </summary>
    private static string? ResolveStageKey(PatchOp op, AuthoredWorkflow current)
    {
        if (!string.IsNullOrEmpty(op.Path))
        {
            var parts = op.Path.TrimStart('/').Split('/');
            if (parts.Length >= 2 &&
                parts[0].Equals("stages", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(parts[1], out var index))
                    return index >= 0 && index < current.Stages.Count
                        ? current.Stages[index].StageKey
                        : null;
                return parts[1]; // treat as a literal stage key
            }
        }

        if (op.Value.HasValue)
        {
            try { return op.Value.Value.Deserialize<AuthoredStage>(ReadOptions)?.StageKey; }
            catch { /* fall through */ }
        }

        return null;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static T? TryDeserialize<T>(JsonElement element)
    {
        try { return element.Deserialize<T>(ReadOptions); }
        catch { return default; }
    }

    private static ProjectionDiagnostic Err(string code, string message, string? stageKey) =>
        new() { Severity = DiagnosticSeverity.Error, Code = code, Message = message, StageKey = stageKey };
}
