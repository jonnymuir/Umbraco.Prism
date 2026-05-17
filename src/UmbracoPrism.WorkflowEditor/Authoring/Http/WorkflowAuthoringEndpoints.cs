using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UmbracoPrism.WorkflowEditor.Authoring.Http;

/// <summary>
/// Registers the workflow authoring HTTP API surface onto an <see cref="IEndpointRouteBuilder"/>.
/// All routes are prefixed <c>/api/workflow-authoring</c>.
///
/// Call this after the existing routing setup, e.g.:
/// <code>app.MapWorkflowAuthoringEndpoints();</code>
///
/// Register backing services via <see cref="WorkflowAuthoringServiceExtensions.AddWorkflowAuthoring"/>.
/// </summary>
public static class WorkflowAuthoringEndpoints
{
    // Read options — lenient for incoming request bodies.
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Maps all <c>/api/workflow-authoring/*</c> Minimal API endpoints.
    /// CORS (AllowAnyOrigin) is applied to the group when the host environment is Development.
    /// </summary>
    public static IEndpointRouteBuilder MapWorkflowAuthoringEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workflow-authoring");

        // Apply Development CORS for the editor host page (Isabelle's origin).
        var env = app.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (env.IsDevelopment())
            group.RequireCors("WorkflowAuthoringDevCors");

        // ── GET /api/workflow-authoring/workflows ─────────────────────────────

        group.MapGet("/workflows", async (IAuthoredWorkflowStore store, CancellationToken ct) =>
        {
            var keys = await store.ListKeysAsync(ct);
            var summaries = new List<object>(keys.Count);
            foreach (var key in keys)
            {
                var wf = await store.LoadAsync(key, ct);
                if (wf != null)
                    summaries.Add(new { id = wf.Id, definitionKey = wf.DefinitionKey, displayName = wf.DisplayName });
            }
            return Results.Json(summaries, WorkflowProjector.CanonicalOptions);
        });

        // ── GET /api/workflow-authoring/workflows/{key} ───────────────────────

        group.MapGet("/workflows/{key}", async (string key, IAuthoredWorkflowStore store, CancellationToken ct) =>
        {
            var wf = await store.LoadAsync(key, ct);
            return wf is null
                ? Results.NotFound(new { error = $"Workflow '{key}' not found." })
                : Results.Json(wf, WorkflowProjector.CanonicalOptions);
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

        // ── POST /api/workflow-authoring/workflows/{key}/preview ──────────────

        group.MapPost("/workflows/{key}/preview", async (
            string key,
            HttpContext ctx,
            IAuthoredWorkflowStore store,
            IWorkflowPatchService patchService,
            IWorkflowPreviewService previewService,
            CancellationToken ct) =>
        {
            var envelope = await ReadBodyAsync<ProposalEnvelope>(ctx, ct);
            if (envelope is null) return Results.BadRequest(new { error = "Request body must be a valid ProposalEnvelope." });

            var original = await store.LoadAsync(key, ct);
            if (original is null) return Results.NotFound(new { error = $"Workflow '{key}' not found." });

            var patchResult = patchService.Apply(envelope, original);
            if (patchResult.HasErrors)
                return Results.Json(new { hasErrors = true, diagnostics = patchResult.Diagnostics }, WorkflowProjector.CanonicalOptions);

            var preview = previewService.Preview(original, patchResult.Updated);
            return Results.Json(preview, WorkflowProjector.CanonicalOptions);
        });

        // ── POST /api/workflow-authoring/workflows/{key}/apply ────────────────

        group.MapPost("/workflows/{key}/apply", async (
            string key,
            HttpContext ctx,
            IAuthoredWorkflowStore store,
            IWorkflowPatchService patchService,
            IHostEnvironment env2,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("WorkflowAuthoringEndpoints");
            var request = await ReadBodyAsync<ApplyWorkflowRequest>(ctx, ct);
            if (request is null) return Results.BadRequest(new { error = "Request body must be { envelope: ProposalEnvelope, approver: string }." });
            if (string.IsNullOrWhiteSpace(request.Approver)) return Results.BadRequest(new { error = "'approver' is required." });

            var original = await store.LoadAsync(key, ct);
            if (original is null) return Results.NotFound(new { error = $"Workflow '{key}' not found." });

            var patchResult = patchService.Apply(request.Envelope, original);
            if (patchResult.HasErrors)
                return Results.Json(new { hasErrors = true, diagnostics = patchResult.Diagnostics }, WorkflowProjector.CanonicalOptions);

            var savedPath = await store.SaveAsync(patchResult.Updated, ct);

            // Write provenance record — fire-and-forget-safe (errors logged, never surface to caller)
            var provenancePath = await WriteProvenanceAsync(
                env2.ContentRootPath, key, request.Envelope, request.Approver, logger, ct);

            logger.LogInformation(
                "Workflow authoring apply: key={Key} approver={Approver} envelopeId={EnvelopeId} savedPath={SavedPath} provenance={Provenance}",
                key, request.Approver, request.Envelope.Id, savedPath, provenancePath ?? "(none)");

            return Results.Json(new
            {
                updated       = patchResult.Updated,
                savedPath,
                provenancePath
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

    private static async Task<string?> WriteProvenanceAsync(
        string contentRootPath,
        string workflowKey,
        ProposalEnvelope envelope,
        string approver,
        ILogger logger,
        CancellationToken ct)
    {
        try
        {
            var provenanceDir = Path.Combine(contentRootPath, "workflow-authored", ".provenance");
            Directory.CreateDirectory(provenanceDir);

            // File-system-safe UTC timestamp: replace colons with hyphens.
            var utcStamp   = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH-mm-ssZ");
            var fileName   = $"{workflowKey}-{utcStamp}.json";
            var filePath   = Path.Combine(provenanceDir, fileName);

            var record = new
            {
                workflowKey,
                appliedAt  = DateTimeOffset.UtcNow,
                approver,
                envelopeId = envelope.Id,
                rationale  = envelope.Rationale
            };

            await using var stream = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, record, WorkflowProjector.CanonicalOptions, ct);

            return filePath;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to write provenance record for workflow '{Key}'.", workflowKey);
            return null;
        }
    }
}

/// <summary>Request body for the <c>POST /workflows/{key}/apply</c> endpoint.</summary>
public record ApplyWorkflowRequest
{
    public required ProposalEnvelope Envelope { get; init; }
    public required string Approver { get; init; }
}
