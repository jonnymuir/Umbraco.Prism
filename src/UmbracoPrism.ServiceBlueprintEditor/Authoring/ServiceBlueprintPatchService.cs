using System.Text.Json;

namespace UmbracoPrism.ServiceBlueprintEditor.Authoring;

/// <summary>
/// Applies a <see cref="ProposalEnvelope"/> to an <see cref="AuthoredServiceBlueprint"/> immutably.
/// Each op is applied in sequence; the first error aborts the chain and returns the original.
/// The final result is validated through <see cref="IServiceBlueprintProjector"/> before being returned.
/// </summary>
public sealed class ServiceBlueprintPatchService : IServiceBlueprintPatchService
{
    // Lenient read options for deserializing op values from the JSON envelope.
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
        // Enum converters are declared directly on TouchpointKind via [JsonConverter].
    };

    private readonly IServiceBlueprintProjector _projector;

    public ServiceBlueprintPatchService(IServiceBlueprintProjector projector) => _projector = projector;

    /// <inheritdoc/>
    public PatchResult Apply(ProposalEnvelope envelope, AuthoredServiceBlueprint original)
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

    private static (AuthoredServiceBlueprint, List<ProjectionDiagnostic>) ApplyOp(
        PatchOp op, AuthoredServiceBlueprint current, PatchPlacement? placement) =>
        op.Op switch
        {
            "insert-touchpoint"   => ApplyInsertTouchpoint(op, current, placement),
            "remove-touchpoint"   => ApplyRemoveTouchpoint(op, current),
            "update-touchpoint"   => ApplyUpdateTouchpoint(op, current),
            "insert-handoff" => ApplyInsertHandoff(op, current),
            "add-route"      => ApplyAddRoute(op, current),
            "update-route"   => ApplyUpdateRoute(op, current),
            "delete-route"   => ApplyDeleteRoute(op, current),
            _ => (current, [Err("PATCH001", $"Unknown op '{op.Op}'.", null)])
        };

    // ─── insert-touchpoint ────────────────────────────────────────────────────────

    private static (AuthoredServiceBlueprint, List<ProjectionDiagnostic>) ApplyInsertTouchpoint(
        PatchOp op, AuthoredServiceBlueprint current, PatchPlacement? placement)
    {
        if (op.Value is null)
            return (current, [Err("PATCH002", "insert-touchpoint requires a value.", null)]);

        AuthoredTouchpoint? touchpoint = TryDeserialize<AuthoredTouchpoint>(op.Value.Value);
        if (touchpoint is null)
            return (current, [Err("PATCH002", "insert-touchpoint value is not a valid AuthoredTouchpoint.", null)]);

        if (string.IsNullOrWhiteSpace(touchpoint.TouchpointKey))
            return (current, [Err("PATCH003", "insert-touchpoint value must have a non-empty TouchpointKey.", null)]);

        var touchpoints = current.Touchpoints.ToList();
        int insertAt = touchpoints.Count; // default: append

        // op-level before/after take precedence over envelope-level placement
        var before = op.Before ?? placement?.InsertBeforeTouchpointKey;
        var after  = op.After  ?? placement?.InsertAfterTouchpointKey;

        if (before != null)
        {
            var idx = touchpoints.FindIndex(s => s.TouchpointKey == before);
            if (idx < 0)
                return (current, [Err("PATCH004", $"insert-touchpoint: 'before' touchpoint '{before}' not found.", null)]);
            insertAt = idx;
        }
        else if (after != null)
        {
            var idx = touchpoints.FindIndex(s => s.TouchpointKey == after);
            if (idx < 0)
                return (current, [Err("PATCH004", $"insert-touchpoint: 'after' touchpoint '{after}' not found.", null)]);
            insertAt = idx + 1;
        }

        touchpoints.Insert(insertAt, touchpoint);
        return (current with { Touchpoints = touchpoints }, []);
    }

    // ─── remove-touchpoint ────────────────────────────────────────────────────────

    private static (AuthoredServiceBlueprint, List<ProjectionDiagnostic>) ApplyRemoveTouchpoint(
        PatchOp op, AuthoredServiceBlueprint current)
    {
        var touchpointKey = ResolveTouchpointKey(op, current);
        if (touchpointKey is null)
            return (current, [Err("PATCH005", "remove-touchpoint: cannot determine target touchpoint from path or value.", null)]);

        var touchpoints = current.Touchpoints.ToList();
        var removed = touchpoints.RemoveAll(s => s.TouchpointKey == touchpointKey);
        if (removed == 0)
            return (current, [Err("PATCH006", $"remove-touchpoint: touchpoint '{touchpointKey}' not found.", touchpointKey)]);

        return (current with { Touchpoints = touchpoints }, []);
    }

    // ─── update-touchpoint ────────────────────────────────────────────────────────

    private static (AuthoredServiceBlueprint, List<ProjectionDiagnostic>) ApplyUpdateTouchpoint(
        PatchOp op, AuthoredServiceBlueprint current)
    {
        if (op.Value is null)
            return (current, [Err("PATCH002", "update-touchpoint requires a value.", null)]);

        AuthoredTouchpoint? updated = TryDeserialize<AuthoredTouchpoint>(op.Value.Value);
        if (updated is null)
            return (current, [Err("PATCH002", "update-touchpoint value is not a valid AuthoredTouchpoint.", null)]);

        // Prefer path-derived key; fall back to the key on the value itself.
        var touchpointKey = ResolveTouchpointKey(op, current) ?? updated.TouchpointKey;
        var touchpoints = current.Touchpoints.ToList();
        var idx = touchpoints.FindIndex(s => s.TouchpointKey == touchpointKey);
        if (idx < 0)
            return (current, [Err("PATCH006", $"update-touchpoint: touchpoint '{touchpointKey}' not found.", touchpointKey)]);

        touchpoints[idx] = updated;
        return (current with { Touchpoints = touchpoints }, []);
    }

    // ─── insert-handoff ──────────────────────────────────────────────────────

    private static (AuthoredServiceBlueprint, List<ProjectionDiagnostic>) ApplyInsertHandoff(
        PatchOp op, AuthoredServiceBlueprint current)
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

    private static (AuthoredServiceBlueprint, List<ProjectionDiagnostic>) ApplyAddRoute(
        PatchOp op, AuthoredServiceBlueprint current)
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

    private static (AuthoredServiceBlueprint, List<ProjectionDiagnostic>) ApplyUpdateRoute(
        PatchOp op, AuthoredServiceBlueprint current)
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

    private static (AuthoredServiceBlueprint, List<ProjectionDiagnostic>) ApplyDeleteRoute(
        PatchOp op, AuthoredServiceBlueprint current)
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
    /// Resolves a touchpoint key from a JSON-Pointer path (e.g. <c>/touchpoints/declaration</c> or <c>/touchpoints/2</c>)
    /// or falls back to the touchpoint key embedded in the op value.
    /// </summary>
    private static string? ResolveTouchpointKey(PatchOp op, AuthoredServiceBlueprint current)
    {
        if (!string.IsNullOrEmpty(op.Path))
        {
            var parts = op.Path.TrimStart('/').Split('/');
            if (parts.Length >= 2 &&
                parts[0].Equals("touchpoints", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(parts[1], out var index))
                    return index >= 0 && index < current.Touchpoints.Count
                        ? current.Touchpoints[index].TouchpointKey
                        : null;
                return parts[1]; // treat as a literal touchpoint key
            }
        }

        if (op.Value.HasValue)
        {
            try { return op.Value.Value.Deserialize<AuthoredTouchpoint>(ReadOptions)?.TouchpointKey; }
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

    private static ProjectionDiagnostic Err(string code, string message, string? touchpointKey) =>
        new() { Severity = DiagnosticSeverity.Error, Code = code, Message = message, TouchpointKey = touchpointKey };
}
