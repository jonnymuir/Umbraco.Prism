using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UmbracoPrism.WorkflowEditor.Authoring;

namespace UmbracoPrism.WorkflowEditor.Extensions;

/// <summary>
/// Registers the Prism Workflow Editor HTTP API surface onto an <see cref="IEndpointRouteBuilder"/>.
/// All routes are prefixed <c>/api/workflow-authoring</c>.
///
/// Call this after the existing routing setup, e.g.:
/// <code>app.MapPrismWorkflowEditor();</code>
///
/// Register backing services via <see cref="WorkflowEditorServiceExtensions.AddPrismWorkflowEditor"/>.
/// </summary>
public static class WorkflowEditorEndpointExtensions
{
    // Read options — lenient for incoming request bodies.
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    // Safe authoring key charset. Endpoint layer rejects anything outside this set with 400
    // before any IO or store lookup; filesystem stores apply a second path-containment guard.
    private static readonly Regex WorkflowKeyPattern = new("^[a-zA-Z0-9_-]+$", RegexOptions.Compiled);

    private static bool IsSafeWorkflowKey(string key) =>
        !string.IsNullOrWhiteSpace(key) && WorkflowKeyPattern.IsMatch(key);

    private static IResult InvalidKey(string key) =>
        Results.BadRequest(new { error = $"Workflow key '{key}' is not a valid authoring key. Allowed characters: letters, digits, '-', '_'." });

    /// <summary>
    /// Maps all <c>/api/workflow-authoring/*</c> Minimal API endpoints.
    /// CORS (AllowAnyOrigin) is applied to the group when the host environment is Development.
    /// </summary>
    public static IEndpointRouteBuilder MapPrismWorkflowEditor(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workflow-authoring");

        // Every authoring endpoint requires an authenticated workflow author. Hosts wire the
        // policy in DI (see WorkflowAuthoringPolicies.WorkflowAuthor); the editor never trusts
        // identity from the request body.
        group.RequireAuthorization(WorkflowAuthoringPolicies.WorkflowAuthor);

        // Apply Development CORS for the editor host page (Isabelle's origin).
        var env = app.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (env.IsDevelopment())
            group.RequireCors("WorkflowAuthoringDevCors");

        // ── GET /api/workflow-authoring/action-catalog ────────────────────────

        group.MapGet("/action-catalog", (IActionCatalogSource catalogSource)
            => Results.Json(catalogSource.GetCatalog(), WorkflowProjector.CanonicalOptions));

        // ── GET /api/workflow-authoring/workflows ─────────────────────────────

        group.MapGet("/workflows", async (IAuthoredWorkflowStore store, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("WorkflowAuthoringEndpoints");
            var entries = await store.ListAsync(ct);
            var summaries = new List<object>(entries.Count);
            foreach (var entry in entries)
            {
                if (!entry.IsLoadable)
                {
                    if (!string.IsNullOrWhiteSpace(entry.ErrorMessage))
                    {
                        logger.LogWarning(
                            "Skipping invalid authored workflow document for key '{WorkflowKey}'. {Reason}",
                            entry.WorkflowKey,
                            entry.ErrorMessage);
                    }

                    continue;
                }

                var loadResult = await TryLoadWorkflowAsync(entry.WorkflowKey, store, logger, ct);
                if (loadResult.Workflow != null)
                    summaries.Add(new
                    {
                        workflowKey = entry.WorkflowKey,
                        id = loadResult.Workflow.Id,
                        definitionKey = loadResult.Workflow.DefinitionKey,
                        displayName = loadResult.Workflow.DisplayName
                    });
            }
            return Results.Json(summaries, WorkflowProjector.CanonicalOptions);
        });

        // ── GET /api/workflow-authoring/workflows/{key} ───────────────────────

        group.MapGet("/workflows/{key}", async (string key, IAuthoredWorkflowStore store, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("WorkflowAuthoringEndpoints");
            var loadResult = await TryLoadWorkflowAsync(key, store, logger, ct);
            if (loadResult.ErrorMessage != null)
                return Results.Conflict(new { error = loadResult.ErrorMessage });

            return loadResult.Workflow is null
                ? Results.NotFound(new { error = $"Workflow '{key}' not found." })
                : Results.Json(loadResult.Workflow, WorkflowProjector.CanonicalOptions);
        });

        group.MapGet("/workflows/{key}/load", async (string key, IAuthoredWorkflowStore store, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("WorkflowAuthoringEndpoints");
            var loadResult = await TryLoadWorkflowAsync(key, store, logger, ct);
            if (loadResult.ErrorMessage != null)
                return Results.Conflict(new { error = loadResult.ErrorMessage });

            return loadResult.Workflow is null
                ? Results.NotFound(new { error = $"Workflow '{key}' not found." })
                : Results.Json(loadResult.Workflow, WorkflowProjector.CanonicalOptions);
        });

        // ── POST /api/workflow-authoring/workflows/{key}/validate ─────────────

        group.MapPost("/workflows/{key}/validate", async (
            string key,
            HttpContext ctx,
            IWorkflowProjector projector,
            CancellationToken ct) =>
        {
            var authored = await ReadBodyAsync<AuthoredWorkflow>(ctx, ct);
            if (authored is null) return Results.BadRequest(new { error = "Request body must be a valid AuthoredWorkflow." });

            var result = projector.Project(authored);
            return Results.Json(new
            {
                hasErrors   = result.HasErrors,
                diagnostics = result.Diagnostics
            }, WorkflowProjector.CanonicalOptions);
        });

        // ── POST /api/workflow-authoring/workflows/{key}/project ──────────────

        group.MapPost("/workflows/{key}/project", async (
            string key,
            HttpContext ctx,
            IWorkflowProjector projector,
            CancellationToken ct) =>
        {
            var authored = await ReadBodyAsync<AuthoredWorkflow>(ctx, ct);
            if (authored is null) return Results.BadRequest(new { error = "Request body must be a valid AuthoredWorkflow." });

            var result = projector.Project(authored);
            return Results.Json(new
            {
                file        = result.File,
                checksum    = result.Checksum,
                diagnostics = result.Diagnostics,
                hasErrors   = result.HasErrors
            }, WorkflowProjector.CanonicalOptions);
        });

        // ── POST /api/workflow-authoring/workflows/{key}/publish ──────────────
        // Canonical direct save: persist the supplied AuthoredWorkflow and re-publish
        // the runtime definition. Use /apply for envelope-mediated saves (PatchOps +
        // provenance). The earlier /save alias was retired in Slice 8a.

        group.MapPost("/workflows/{key}/publish", async (
            string key,
            HttpContext ctx,
            IAuthoredWorkflowStore store,
            IWorkflowPublishService publishService,
            CancellationToken ct) =>
        {
            if (!IsSafeWorkflowKey(key)) return InvalidKey(key);
            var authored = await ReadBodyAsync<AuthoredWorkflow>(ctx, ct);
            return authored is null
                ? Results.BadRequest(new { error = "Request body must be a valid AuthoredWorkflow." })
                : await SaveAndPublishAsync(key, authored, store, publishService, ct);
        });

        // ── POST /api/workflow-authoring/workflows/{key}/simulate ─────────────

        group.MapPost("/workflows/{key}/simulate", async (
            string key,
            HttpContext ctx,
            IAuthoredWorkflowStore store,
            IWorkflowSimulationService simulationService,
            CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<SimulateWorkflowRequest>(ctx, ct);
            var workflow = request?.Workflow ?? await store.LoadAsync(key, ct);
            if (workflow is null)
                return Results.NotFound(new { error = $"Workflow '{key}' not found." });

            if (request?.Workflow is not null
                && !string.Equals(key, workflow.DefinitionKey, StringComparison.Ordinal))
                return Results.BadRequest(new { error = $"Route key '{key}' must match workflow definitionKey '{workflow.DefinitionKey}'." });

            var result = simulationService.Simulate(workflow, request?.Actions, request?.MaxSteps);
            return Results.Json(result, WorkflowProjector.CanonicalOptions);
        });

        // ── POST /api/workflow-authoring/workflows/{key}/apply ────────────────

        group.MapPost("/workflows/{key}/apply", async (
            string key,
            HttpContext ctx,
            IAuthoredWorkflowStore store,
            IWorkflowPatchService patchService,
            IWorkflowPublishService publishService,
            IWorkflowAuthoringProvenanceStore provenanceStore,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("WorkflowAuthoringEndpoints");

            if (!IsSafeWorkflowKey(key)) return InvalidKey(key);

            var request = await ReadBodyAsync<ApplyWorkflowRequest>(ctx, ct);
            if (request is null) return Results.BadRequest(new { error = "Request body must be { envelope: ProposalEnvelope }." });

            // Envelope-mode saves must carry at least one operation; otherwise the
            // caller wants direct save (use /publish) rather than envelope mediation.
            if (request.Envelope.Ops is null || request.Envelope.Ops.Count == 0)
                return Results.BadRequest(new { error = "envelope.ops must contain at least one operation. Use /publish for whole-document saves." });

            // Approver is bound to the authenticated principal. RequireAuthorization on the
            // group has already rejected anonymous calls; if we still cannot resolve a name
            // claim treat it as an authentication failure rather than silently writing "".
            var approver = ResolvePrincipalIdentity(ctx.User);
            if (string.IsNullOrWhiteSpace(approver))
                return Results.Unauthorized();

            // Agent is optional. When omitted, synthesise one from the authenticated
            // principal so provenance still records who applied the envelope.
            var envelope = request.Envelope;
            if (envelope.Agent is null)
            {
                envelope = envelope with
                {
                    Agent = new PatchAgent { Kind = "human-assisted", Identity = approver }
                };
            }
            else if (string.IsNullOrWhiteSpace(envelope.Agent.Kind))
            {
                return Results.BadRequest(new { error = "envelope.agent.kind must be a non-blank actor label when an agent is supplied." });
            }
            else if (string.Equals(envelope.Agent.Kind, "human-assisted", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(envelope.Agent.Identity, approver, StringComparison.OrdinalIgnoreCase))
            {
                // Cross-stamp: a human-assisted envelope must name the calling principal.
                // Agent-kind envelopes that name a non-human actor (e.g. github-copilot)
                // are deliberately not cross-checked here.
                return Results.BadRequest(new
                {
                    error = "envelope.agent.identity must match the authenticated principal for human-assisted proposals."
                });
            }

            var original = await store.LoadAsync(key, ct);
            if (original is null) return Results.NotFound(new { error = $"Workflow '{key}' not found." });

            var patchResult = patchService.Apply(envelope, original);
            if (patchResult.HasErrors)
                return Results.Json(new { hasErrors = true, diagnostics = patchResult.Diagnostics }, WorkflowProjector.CanonicalOptions);

            var savedPath = await store.SaveAsync(key, patchResult.Updated, ct);
            var publishResult = await publishService.PublishAsync(patchResult.Updated, ct);

            var provenancePath = await SaveProvenanceAsync(
                key, envelope, approver, provenanceStore, logger, ct);

            logger.LogInformation(
                "Workflow authoring apply: key={Key} approver={Approver} envelopeId={EnvelopeId} savedPath={SavedPath} provenance={Provenance}",
                key, approver, envelope.Id, savedPath, provenancePath ?? "(none)");

            return Results.Json(new
            {
                updated       = patchResult.Updated,
                savedPath,
                provenancePath,
                publish = publishResult
            }, WorkflowProjector.CanonicalOptions);
        });

        return app;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static async Task<T?> ReadBodyAsync<T>(HttpContext ctx, CancellationToken ct)
    {
        try { return await ctx.Request.ReadFromJsonAsync<T>(ReadOptions, ct); }
        catch { return default; }
    }

    /// <summary>
    /// Resolves the canonical "who is publishing" identity from the calling principal.
    /// Mirrors <c>PrismIdentityExtensions.GetEmail</c>: prefers <c>preferred_username</c>, falls
    /// back to <see cref="ClaimTypes.Email"/>, then <see cref="ClaimTypes.Name"/> /
    /// <see cref="ClaimsIdentity.Name"/>. Returns null when no usable claim is present.
    /// </summary>
    internal static string? ResolvePrincipalIdentity(ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true) return null;
        return user.FindFirst("preferred_username")?.Value
            ?? user.FindFirst(ClaimTypes.Email)?.Value
            ?? user.FindFirst(ClaimTypes.Name)?.Value
            ?? user.Identity?.Name;
    }

    private static async Task<AuthoredWorkflowLoadResult> TryLoadWorkflowAsync(
        string key,
        IAuthoredWorkflowStore store,
        ILogger logger,
        CancellationToken ct)
    {
        try
        {
            return new(await store.LoadAsync(key, ct), null);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex,
                "Skipping invalid authored workflow document for key '{WorkflowKey}'.",
                key);

            return new(
                null,
                $"Workflow '{key}' exists, but its editor definition is invalid. Repair the editor source before opening it in the reference editor.");
        }
    }

    private static async Task<IResult> SaveAndPublishAsync(
        string key,
        AuthoredWorkflow authored,
        IAuthoredWorkflowStore store,
        IWorkflowPublishService publishService,
        CancellationToken ct)
    {
        var savedPath = await store.SaveAsync(key, authored, ct);
        var result = await publishService.PublishAsync(authored, ct);
        return Results.Json(result with { SavedPath = savedPath }, WorkflowProjector.CanonicalOptions);
    }

    private static async Task<string?> SaveProvenanceAsync(
        string workflowKey,
        ProposalEnvelope envelope,
        string approver,
        IWorkflowAuthoringProvenanceStore provenanceStore,
        ILogger logger,
        CancellationToken ct)
    {
        try
        {
            return await provenanceStore.SaveAsync(workflowKey, envelope, approver, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to write provenance record for workflow '{Key}'.", workflowKey);
            return null;
        }
    }

    private sealed record SimulateWorkflowRequest
    {
        public AuthoredWorkflow? Workflow { get; init; }

        public IReadOnlyList<string>? Actions { get; init; }

        public int? MaxSteps { get; init; }
    }

    private sealed record AuthoredWorkflowLoadResult(AuthoredWorkflow? Workflow, string? ErrorMessage);
}
