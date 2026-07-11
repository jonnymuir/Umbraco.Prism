using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using UmbracoPrism.Core.Extensions;
using UmbracoPrism.MockBusinessApp.Services;
using UmbracoPrism.MockBusinessApp.Services.Publishing;
using UmbracoPrism.MockBusinessApp.Services.WorkflowActions;
using UmbracoPrism.Shared.Extensions;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.Shared.Services.Sanitization;
using UmbracoPrism.WorkflowEditor.Extensions;
using UmbracoPrism.WorkflowRuntime.Abstractions;
using UmbracoPrism.WorkflowRuntime.Api;
using UmbracoPrism.WorkflowRuntime.Extensions;
using UmbracoPrism.WorkflowRuntime.Mcp;
using UmbracoPrism.WorkflowRuntime.Services;

var builder = WebApplication.CreateBuilder(args);

// Local secrets override — gitignored. Supply real Entra tenant/client IDs and member
// emails here. See src/UmbracoPrism.MockBusinessApp/README.md for setup instructions.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddPrismAuthentication(builder.Configuration);

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

// MockBusinessApp serves controlled seed content only — passthrough sanitizer is sufficient.
// The real GDS allowlist sanitizer (WorkflowContentSanitizer) is wired up in TestSite via Core.
builder.Services.AddSingleton<IWorkflowContentSanitizer, PassthroughSanitizer>();

// The reference app keeps the demo workflows in memory. `/mockapp/workflows/*` (the
// editor's own save endpoint) and the AI/tooling authoring surface below share this same
// IWorkflowSourceStore — they used to be two separate stores that both silently mutated
// the live engine with no idea the other existed; unified so a save from either surface
// is immediately visible to both (InMemoryRuntimePublishedWorkflowStore.SaveAsync calls
// engine.UpdateDefinition). See MapPrismWorkflowAuthoringApi()/MapPrismWorkflowAuthoringMcp() below.
builder.Services.AddSingleton<IWorkflowSourceStore, InMemoryRuntimePublishedWorkflowStore>();
builder.Services.AddPrismWorkflowAuthoring();
builder.Services.AddPrismWorkflowAuthoringMcp();

// Editor library — projector / patch / simulation / action catalog only.
builder.Services.AddPrismWorkflowEditor();
// Publish service moved into MockBusinessApp in Slice B (it was always host-policy code).
builder.Services.AddSingleton<IWorkflowPublishService, WorkflowPublishService>();
builder.Services.AddBusinessAppWorkflowActions();

// Business App workflow engine — singleton so in-memory instance state survives across requests.
// The reference app uses ReferenceWorkflowDefinitionStore to seed exactly 4 workflows at runtime.
// Downstream apps can use FilesystemWorkflowDefinitionStore or their own IWorkflowDefinitionStore.
builder.Services.AddSingleton<IWorkflowDefinitionStore, ReferenceWorkflowDefinitionStore>();
builder.Services.AddSingleton<UmbracoPrism.MockBusinessApp.Services.MoneyModeller.MemberRecordService>();
builder.Services.AddSingleton<BusinessAppWorkflowEngine>();
builder.Services.AddSingleton<IWorkflowRuntimeEngine>(sp => sp.GetRequiredService<BusinessAppWorkflowEngine>());
builder.Services.AddHostedService<WorkflowTuiService>();

var app = builder.Build();

// Serve the Vite-built workflow-editor.html (and its JS/CSS assets) from the WorkflowEditor wwwroot/dist
// output directory. This lets the walkthrough spec navigate to /workflow-editor.html on this host.
var distPath = Path.GetFullPath(
    Path.Combine(builder.Environment.ContentRootPath, "..", "UmbracoPrism.WorkflowEditor", "wwwroot", "dist"));
if (Directory.Exists(distPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(distPath),
        RequestPath = "",
        OnPrepareResponse = ctx =>
        {
            ctx.Context.Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
            ctx.Context.Response.Headers.Pragma = "no-cache";
            ctx.Context.Response.Headers.Expires = "0";
        }
    });
}

app.MapGet("/workflow-editor", (HttpRequest request) =>
{
    var workflowKey = request.Query["workflow"].ToString();
    var targetWorkflow = string.IsNullOrWhiteSpace(workflowKey) ? "planning" : workflowKey;

    return Results.Redirect($"/workflow-editor.html?workflow={Uri.EscapeDataString(targetWorkflow)}");
});

// SECURITY: KEYCLOAK_BACKCHANNEL_URL must never be set in production — it bypasses
// TLS certificate validation for OIDC metadata fetches, which is only acceptable
// in controlled development environments. Fail loudly if misconfigured.
if (!app.Environment.IsDevelopment() && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KEYCLOAK_BACKCHANNEL_URL")))
{
    throw new InvalidOperationException("KEYCLOAK_BACKCHANNEL_URL must not be set in non-Development environments.");
}

// SECURITY: Admin workflow endpoints should not exist outside Development mode in
// the reference app. Slice B retired the platform `/api/workflow-authoring` API,
// so only `/admin` is gated here. The MockBusinessApp's `/mockapp/workflows/*`
// endpoints are deliberately anonymous in the reference app — downstream hosts
// add whatever auth their TS WorkflowSource integration needs.
if (!app.Environment.IsDevelopment())
{
    app.Use(async (ctx, next) =>
    {
        if (ctx.Request.Path.StartsWithSegments("/admin"))
        {
            ctx.Response.StatusCode = 404;
            return;
        }
        await next();
    });
}

app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/api/backoffice/me", StringComparison.OrdinalIgnoreCase))
    {
        app.Logger.LogInformation(
            "BusinessApp arrival before auth: {Method} {Path} trace={TraceIdentifier} authHeaderPresent={AuthHeaderPresent} callerTraceId={CallerTraceId}",
            ctx.Request.Method,
            ctx.Request.Path.Value ?? "/",
            ctx.TraceIdentifier,
            ctx.Request.Headers.ContainsKey("Authorization"),
            GetCallerTraceId(ctx.Request));
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

// Slice B: TS `WorkflowSource` HTTP integration. The reference app exposes the
// four demo workflows over /mockapp/workflows/*. There is intentionally NO auth
// on these endpoints — the reference app proves the editor boundary works
// without inheriting authoring policies. Real downstream apps add their own
// authentication/authorization here (e.g. require an Entra group, a tenant
// claim, or a session cookie) before exposing this surface in production.
var mockWorkflowJsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
    AllowOutOfOrderMetadataProperties = true,
};

// AI/tooling authoring API — list/read/validate/save/simulate workflow definitions against
// the live IWorkflowSourceStore above. Intentionally NO auth here either, for the same
// reference-app reason as the block below: real downstream apps chain their own
// .RequireAuthorization() (or any other policy) onto the returned route group before
// exposing this to anything beyond localhost. Same story for the MCP endpoint — an AI
// agent (e.g. Claude Code via `claude mcp add --transport http`) calls the same
// WorkflowAuthoringService in-process, so a save reaches the live engine immediately.
app.MapPrismWorkflowAuthoringApi();
app.MapPrismWorkflowAuthoringMcp();

app.MapGet("/mockapp/workflows", async (IWorkflowSourceStore store, CancellationToken ct) =>
    Results.Json(await store.ListAsync(ct), mockWorkflowJsonOptions));

app.MapGet("/mockapp/workflows/{key}", async (string key, IWorkflowSourceStore store, CancellationToken ct) =>
{
    if (!System.Text.RegularExpressions.Regex.IsMatch(key, @"^[a-zA-Z0-9_\-]+$"))
    {
        return Results.Problem(
            detail: $"Workflow key '{key}' contains characters that are not allowed.",
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid workflow key");
    }
    var workflow = await store.LoadAsync(key, ct);
    return workflow is null
        ? Results.NotFound()
        : Results.Json(workflow, mockWorkflowJsonOptions);
});

app.MapPut("/mockapp/workflows/{key}", async (string key, HttpContext ctx, IWorkflowSourceStore store, WorkflowAuthoringService authoringService, ILogger<Program> logger) =>
{
    if (!System.Text.RegularExpressions.Regex.IsMatch(key, @"^[a-zA-Z0-9_\-]+$"))
    {
        return Results.Problem(
            detail: $"Workflow key '{key}' contains characters that are not allowed.",
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid workflow key");
    }

    var parseResult = await WorkflowSourceSaveRequestParser.ParseAsync(ctx, mockWorkflowJsonOptions, authoringService, ctx.RequestAborted);
    if (parseResult.Problem is not null)
    {
        return WorkflowSourceSaveRequestParser.ToProblemResult(ctx, parseResult.Problem);
    }

    var workflow = parseResult.Workflow!;

    if (!string.Equals(workflow.DefinitionKey, key, StringComparison.Ordinal))
    {
        return Results.Problem(
            detail: $"Route key '{key}' does not match body definitionKey '{workflow.DefinitionKey}'.",
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid workflow payload");
    }

    // InMemoryRuntimePublishedWorkflowStore.SaveAsync already calls engine.UpdateDefinition —
    // no separate call needed here now that this shares the toolkit's IWorkflowSourceStore.
    // WorkflowSourceSaveRequestParser already validated above, so this calls the store
    // directly rather than WorkflowAuthoringService.SaveAsync (which would just re-validate).
    // workflow.Version — round-tripped by any client that loaded this workflow first — is the
    // optimistic-concurrency expected version; see IWorkflowSourceStore.SaveAsync.
    var saveResult = await store.SaveAsync(workflow, workflow.Version, ctx.RequestAborted);
    if (!saveResult.Saved)
    {
        return Results.Conflict(new
        {
            currentVersion = saveResult.CurrentVersion,
            message = $"Workflow has changed since it was loaded — current version is {saveResult.CurrentVersion}, which didn't match the expected version. Reload and reapply your change."
        });
    }

    return Results.NoContent();
});


app.MapGet("/api/backoffice/me", (IConfiguration config, ClaimsPrincipal user, HttpContext context, ILogger<Program> logger) =>
{
    logger.LogInformation(
        "BusinessApp handler entry: {Method} {Path} trace={TraceIdentifier} authHeaderPresent={AuthHeaderPresent} callerTraceId={CallerTraceId} userAuthenticated={UserAuthenticated}",
        context.Request.Method,
        context.Request.Path.Value ?? "/",
        context.TraceIdentifier,
        context.Request.Headers.ContainsKey("Authorization"),
        GetCallerTraceId(context.Request),
        user.Identity?.IsAuthenticated ?? false);

    var tenant = user.GetPrismTenant(PrismResolvers.FromConfig(config));

    if (tenant == null) return Results.Problem("Tenant not recognised by Business Application.");

    var email = user.GetEmail();

    if (string.IsNullOrEmpty(email)) return Results.Problem("User email claim not found.");

    // Resolve Member (Check email AND tenant ID)
    var members = config.GetSection("PrismBusinessApp:Members").Get<List<BackOfficeMember>>();
    var member = members?.FirstOrDefault(m =>
        m.Email.Equals(email, StringComparison.OrdinalIgnoreCase) &&
        m.TenantCode == tenant.Code);

    return Results.Ok(new
    {
        Tenant = tenant.DisplayName,
        TenantCode = tenant.Code,
        UserEmail = email,
        IsRegistered = member != null,
        BackOfficeId = member?.BackOfficeId ?? "N/A",
        AssignedRole = member?.Role ?? "Guest"
    });
}).RequireAuthorization();

// Workflow API — server-to-server calls from Umbraco TestSite forwarding the member's Bearer token.
// Identity is derived from JWT claims; never trusted from the request body.
app.MapPost("/api/workflow/{workflowKey}/current", async (
    string workflowKey,
    ClaimsPrincipal user,
    IConfiguration config,
    BusinessAppWorkflowEngine engine,
    ILogger<Program> logger,
    HttpContext context) =>
{
    var tenant = user.GetPrismTenant(PrismResolvers.FromConfig(config));
    var email = user.GetEmail();

    if (tenant == null)
        return Results.Problem("Tenant not recognised by Business Application.");
    if (string.IsNullOrEmpty(email))
        return Results.Problem("User email claim not found.");

    // Read optional body parameters
    string? instanceId = null;
    string? action = null;
    
    try
    {
        var body = await context.Request.ReadFromJsonAsync<WorkflowCurrentApiRequest>();
        instanceId = body?.InstanceId;
        action = body?.Action;
    }
    catch
    {
        // Body is optional; empty/null body is fine
    }

    logger.LogInformation("Workflow current: key={Key} tenant={Tenant} user={User} instanceId={InstanceId} action={Action}", 
        workflowKey, tenant.Code, email, instanceId ?? "(none)", action ?? "(none)");

    var envelope = engine.GetCurrent(
        workflowKey,
        tenant.Code,
        email,
        ReferenceWorkflowQueues.WebUserProfile(),
        instanceId,
        action);
    return envelope.ResponseState == "error" ? Results.UnprocessableEntity(envelope) : Results.Ok(envelope);
}).RequireAuthorization();

app.MapPost("/api/workflow/{workflowKey}/advance", (
    string workflowKey,
    WorkflowAdvanceApiRequest request,
    ClaimsPrincipal user,
    IConfiguration config,
    BusinessAppWorkflowEngine engine,
    ILogger<Program> logger) =>
{
    var tenant = user.GetPrismTenant(PrismResolvers.FromConfig(config));
    var email = user.GetEmail();

    if (tenant == null)
        return Results.Problem("Tenant not recognised by Business Application.");
    if (string.IsNullOrEmpty(email))
        return Results.Problem("User email claim not found.");

    logger.LogInformation(
        "Workflow advance: key={Key} instance={Instance} action={Action}",
        workflowKey, request.InstanceId, request.Action);

    var envelope = engine.Advance(
        request.InstanceId,
        tenant.Code,
        email,
        ReferenceWorkflowQueues.WebUserProfile(),
        request.Action,
        request.StateVersion,
        request.FieldValues);

    return envelope.ResponseState == "error" ? Results.UnprocessableEntity(envelope) : Results.Ok(envelope);
}).RequireAuthorization();

app.MapGet("/api/workflow/instances", (
    ClaimsPrincipal user,
    IConfiguration config,
    BusinessAppWorkflowEngine engine,
    ILogger<Program> logger) =>
{
    var tenant = user.GetPrismTenant(PrismResolvers.FromConfig(config));
    var email = user.GetEmail();

    if (tenant == null)
        return Results.Problem("Tenant not recognised by Business Application.");
    if (string.IsNullOrEmpty(email))
        return Results.Problem("User email claim not found.");

    logger.LogInformation("Workflow instances: tenant={Tenant} user={User}", tenant.Code, email);

    var envelope = engine.GetInstances(tenant.Code, email);
    return Results.Ok(envelope);
}).RequireAuthorization();

// SECURITY: Anonymous test-reset endpoint — Development only.
// This endpoint wipes all workflow instances and is intended exclusively for
// integration test setup/teardown. It MUST NOT be reachable in any non-Development
// environment (the global /admin guard above does not match this path, so guard
// the endpoint explicitly here).
app.MapDelete("/api/test/reset", (BusinessAppWorkflowEngine engine, ILogger<Program> logger, IHostEnvironment env) =>
{
    if (!env.IsDevelopment())
    {
        return Results.NotFound();
    }

    engine.ResetAll();
    logger.LogInformation("Test reset: all workflow instances cleared via /api/test/reset");
    return Results.Ok(new { cleared = true });
});

// ── Debug / diagnostics (Development only, no auth) ─────────────────────────
// curl https://localhost:7245/debug/auth  (or http://localhost:5163/debug/auth)
app.MapGet("/debug/auth", (IConfiguration config) =>
{
    if (!app.Environment.IsDevelopment()) return Results.NotFound();

    var tenants = config.GetSection("PrismBusinessApp:Tenants")
        .GetChildren()
        .Select(t => new
        {
            Code             = t["Code"],
            OidcAuthority    = t["OidcAuthority"],
            EntraTenantId    = t["EntraTenantId"],
            ClientId         = t["ClientId"],
        }).ToList();

    var backchannelUrl = Environment.GetEnvironmentVariable("KEYCLOAK_BACKCHANNEL_URL");
    var codespaceName  = Environment.GetEnvironmentVariable("CODESPACE_NAME");
    var aspNetCoreEnv  = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    var isDevelopment  = string.Equals(aspNetCoreEnv, "Development", StringComparison.OrdinalIgnoreCase);
    var backchannelJwksEnabled = isDevelopment && !string.IsNullOrEmpty(backchannelUrl);

    // Probe the backchannel metadata endpoint so we know if it's reachable
    string? backchannelProbe = null;
    if (!string.IsNullOrEmpty(backchannelUrl))
    {
        try
        {
            var oidcPath = tenants.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.OidcAuthority))
                ?.OidcAuthority;
            if (oidcPath != null)
            {
                var metaUrl = $"{backchannelUrl.TrimEnd('/')}{new Uri(oidcPath).AbsolutePath}/.well-known/openid-configuration";
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var resp = http.GetAsync(metaUrl).GetAwaiter().GetResult();
                backchannelProbe = $"{(int)resp.StatusCode} {resp.StatusCode} — {metaUrl}";
            }
        }
        catch (Exception ex)
        {
            backchannelProbe = $"ERROR: {ex.Message}";
        }
    }

    return Results.Ok(new
    {
        environment             = app.Environment.EnvironmentName,
        aspNetCoreEnvironment   = aspNetCoreEnv ?? "(not set)",
        codespaceName           = codespaceName ?? "(not set)",
        backchannelUrl          = backchannelUrl ?? "(not set)",
        backchannelJwksEnabled,
        backchannelProbe,
        tenants,
    });
});

// ── Admin UI (no auth — local dev only) ─────────────────────────────────────

app.MapGet("/admin/workflow", async (BusinessAppWorkflowEngine engine, IWorkflowSourceStore workflowSourceStore) =>
{
    var instances = engine.GetAllInstances().OrderBy(i => i.CreatedAt).ToList();
    var businessQueue = engine.GetQueueWorkItems(ReferenceWorkflowQueues.BusinessUserProfile()).Items;
    var defs = engine.GetAllDefinitions().ToList();
    var defsByKey = defs.ToDictionary(d => d.DefinitionKey, StringComparer.OrdinalIgnoreCase);
    // WorkflowSourceSummary is keyed by definitionKey alone (no separate route/workflow key
    // concept — confirmed identical for every reference seed), so this no longer needs the
    // workflowKey-vs-definitionKey bridging the old ReferenceWorkflowSourceStore.WorkflowSummary required.
    var sourceWorkflowDefinitionKeys = (await workflowSourceStore.ListAsync())
        .Select(entry => entry.DefinitionKey)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    string Esc(string s) => System.Net.WebUtility.HtmlEncode(s);

    var queueRows = businessQueue.Count == 0
        ? """<tr><td colspan="7" style="text-align:center;color:#888;padding:1.5rem">No business-user queue work</td></tr>"""
        : string.Join("\n", businessQueue.Select((item, n) =>
        {
            var shortId = item.InstanceId.Length > 12 ? item.InstanceId[..8] + "…" : item.InstanceId;
            var actions = item.AvailableActions.Count == 0
                ? """<span style="color:#888;font-size:.8rem">No actions</span>"""
                : string.Join(" ", item.AvailableActions.Select(action => $"""
                    <form method="post" action="/admin/workflow/{Esc(item.InstanceId)}/advance" style="display:inline">
                      <input type="hidden" name="action" value="{Esc(action.ActionKey)}" />
                      <input type="hidden" name="stateVersion" value="{item.StateVersion}" />
                      <button class="btn btn-queue-action">{Esc(action.Label)}</button>
                    </form>
                    """));

            return $"""
            <tr data-workflow-key="{Esc(item.WorkflowKey)}" data-queue-name="{Esc(item.QueueName ?? string.Empty)}">
              <td>{n + 1}</td>
              <td style="font-family:monospace;font-size:.8em"><span title="{Esc(item.InstanceId)}">{Esc(shortId)}</span></td>
              <td>
                <strong>{Esc(item.WorkflowDisplayName)}</strong>
                <div style="color:#888;font-size:.73rem">{Esc(item.WorkflowKey)}</div>
              </td>
              <td>
                <span class="badge">{Esc(item.StateDisplayName)}</span>
                <span style="color:#bbb;font-size:.73rem;display:block">{Esc(item.StateKey)}</span>
              </td>
              <td>{Esc(item.QueueName ?? "default")}</td>
              <td>{Esc(item.TenantId)}</td>
              <td class="actions">{actions}</td>
            </tr>
            """;
        }));

    var rows = instances.Count == 0
        ? """<tr><td colspan="6" style="text-align:center;color:#888;padding:1.5rem">No workflow instances</td></tr>"""
        : string.Join("\n", instances.Select((inst, n) =>
        {
            defsByKey.TryGetValue(inst.WorkflowKey, out var def);
            var stateDisplay = def?.States
                .FirstOrDefault(s => string.Equals(s.StateKey, inst.CurrentState, StringComparison.OrdinalIgnoreCase))
                ?.DisplayName ?? inst.CurrentState;
            var shortId = inst.InstanceId.Length > 12 ? inst.InstanceId[..8] + "…" : inst.InstanceId;
            return $"""
            <tr data-workflow-key="{Esc(inst.WorkflowKey)}" data-current-state="{Esc(inst.CurrentState)}">
              <td>{n + 1}</td>
              <td style="font-family:monospace;font-size:.8em"><span title="{Esc(inst.InstanceId)}">{Esc(shortId)}</span></td>
              <td>{Esc(inst.WorkflowKey)}</td>
              <td>
                <span class="badge">{Esc(stateDisplay)}</span>
                <span style="color:#bbb;font-size:.73rem;display:block">{Esc(inst.CurrentState)}</span>
              </td>
              <td>{Esc(inst.TenantId)}</td>
              <td class="actions">
                <form method="post" action="/admin/workflow/{Esc(inst.InstanceId)}/reset" style="display:inline">
                  <button class="btn btn-reset" onclick="return confirm('Remove this instance?')">↺ Reset</button>
                </form>
              </td>
            </tr>
            """;
        }));

    var defRows = defs.Count == 0
        ? """<tr><td colspan="2" style="text-align:center;color:#888;padding:1.5rem">No definitions loaded</td></tr>"""
        : string.Join("\n", defs.Select(def =>
        {
            var authoredWorkflowKey = sourceWorkflowDefinitionKeys.Contains(def.DefinitionKey)
                ? def.DefinitionKey
                : null;
            var editorShortcut = authoredWorkflowKey is not null
                ? $"""<a class="btn btn-edit-workflow" href="/workflow-editor?workflow={Esc(authoredWorkflowKey!)}">↗ Edit workflow</a>"""
                : """<span class="editor-unavailable" title="This workflow currently has no editor definition configured.">No editor definition yet</span>""";
            return $"""
            <tr data-workflow-key="{Esc(authoredWorkflowKey ?? def.DefinitionKey)}" data-definition-key="{Esc(def.DefinitionKey)}">
              <td>
                <strong>{Esc(def.DisplayName)}</strong>
                <div style="color:#888;font-size:.78rem">{Esc(def.DefinitionKey)} v{def.Version}</div>
              </td>
              <td class="actions">{editorShortcut}</td>
            </tr>
            """;
        }));

    var html = $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8"/>
          <meta name="viewport" content="width=device-width,initial-scale=1"/>
          <title>Workflow Admin — MockBusinessApp</title>
          <style>
            *, *::before, *::after { box-sizing: border-box; }
            body { font-family: system-ui, sans-serif; margin: 0; background: #f4f5f7; color: #1a1a2e; }
            header { background: #1a1a2e; color: #fff; padding: 1rem 1.5rem; display:flex; align-items:center; gap:1rem; }
            header h1 { margin:0; font-size:1.1rem; }
            main { padding: 1.5rem; max-width: 1100px; margin: 0 auto; }
            h2 { font-size: .95rem; text-transform: uppercase; letter-spacing:.05em; color:#555; margin-top:2rem; }
            table { width:100%; border-collapse:collapse; background:#fff; border-radius:8px; overflow:hidden; box-shadow:0 1px 3px rgba(0,0,0,.08); }
            th { background:#f0f1f4; text-align:left; padding:.6rem .9rem; font-size:.8rem; text-transform:uppercase; letter-spacing:.04em; color:#555; }
            td { padding:.6rem .9rem; border-top:1px solid #eee; font-size:.88rem; vertical-align:middle; }
            tr:hover td { background:#fafbff; }
            .badge { background:#dbeafe; color:#1d4ed8; padding:.15rem .5rem; border-radius:999px; font-size:.78rem; font-weight:600; }
            .actions { white-space:nowrap; text-align:right; }
            .btn { border:none; border-radius:5px; padding:.25rem .65rem; font-size:.8rem; cursor:pointer; font-weight:600; text-decoration:none; display:inline-block; }
            .btn-queue-action { background:#dcfce7; color:#166534; }
            .btn-queue-action:hover { background:#bbf7d0; }
            .btn-reset { background:#f3f4f6; color:#374151; }
            .btn-reset:hover { background:#e5e7eb; }
            .btn-reset-all { background:#fee2e2; color:#991b1b; padding:.35rem 1rem; }
            .btn-reset-all:hover { background:#fca5a5; }
            .btn-edit-workflow { background:#dbeafe; color:#1d4ed8; }
            .btn-edit-workflow:hover { background:#bfdbfe; }
            .editor-unavailable { display:inline-flex; align-items:center; min-height:30px; padding:.25rem .65rem; border-radius:999px; background:#f3f4f6; color:#4b5563; font-size:.78rem; font-weight:600; }
            .header-links { margin-left:auto; display:flex; gap:.5rem; flex-wrap:wrap; }
            .header-link { display:inline-flex; align-items:center; padding:.45rem .8rem; border-radius:999px; background:rgba(255,255,255,.12); color:#fff; text-decoration:none; font-size:.82rem; font-weight:600; }
            .header-link:hover { background:rgba(255,255,255,.2); }
            .toolbar { margin-bottom:.75rem; display:flex; gap:.5rem; align-items:center; }
            .count { color:#888; font-size:.85rem; }
          </style>
        </head>
        <body>
          <header>
            <h1>🔧 Workflow Admin</h1>
            <span style="opacity:.6;font-size:.85rem">MockBusinessApp — local dev only</span>
            <nav class="header-links" aria-label="Workflow showcase shortcuts">
              <a class="header-link" href="/workflow-editor">Workflow Editor</a>
            </nav>
          </header>
          <main>
            <h2>Business-user Queue</h2>
            <div class="toolbar">
              <span class="count">{{businessQueue.Count}} work item(s)</span>
            </div>
            <table>
              <thead>
                <tr>
                  <th>#</th><th>Instance ID</th><th>Workflow</th><th>Queue step</th><th>Queue</th><th>Tenant</th><th></th>
                </tr>
              </thead>
              <tbody>{{queueRows}}</tbody>
            </table>

            <h2>Workflow Instances</h2>
            <div class="toolbar">
              <span class="count">{{instances.Count}} instance(s)</span>
              <form method="post" action="/admin/workflow/reset-all" style="margin-left:auto">
                <button class="btn btn-reset-all" onclick="return confirm('Remove ALL instances?')">Reset All</button>
              </form>
            </div>
            <table>
              <thead>
                <tr>
                  <th>#</th><th>Instance ID</th><th>Workflow</th><th>State</th><th>Tenant</th><th></th>
                </tr>
              </thead>
              <tbody>{{rows}}</tbody>
            </table>

            <h2>Workflow Definitions</h2>
            <div class="toolbar">
              <span class="count">{{defs.Count}} definition(s)</span>
            </div>
            <table>
              <thead><tr><th>Definition</th><th></th></tr></thead>
              <tbody>{{defRows}}</tbody>
            </table>
          </main>
        </body>
        </html>
        """;

    return Results.Content(html, "text/html");
});

app.MapPost("/admin/workflow/{instanceId}/advance", async (string instanceId, HttpContext context, BusinessAppWorkflowEngine engine) =>
{
    var form = await context.Request.ReadFormAsync();
    var action = form["action"].FirstOrDefault();
    var stateVersionValue = form["stateVersion"].FirstOrDefault();

    if (string.IsNullOrWhiteSpace(action) || !int.TryParse(stateVersionValue, out var stateVersion))
    {
        return Results.BadRequest("Missing queue action details.");
    }

    var instance = engine.GetAllInstances()
        .FirstOrDefault(candidate => string.Equals(candidate.InstanceId, instanceId, StringComparison.Ordinal));

    if (instance is null)
    {
        return Results.NotFound();
    }

    engine.Advance(
        instanceId,
        instance.TenantId,
        instance.UserId,
        ReferenceWorkflowQueues.BusinessUserProfile(),
        action,
        stateVersion,
        fieldValues: null);

    return Results.Redirect("/admin/workflow");
});

app.MapPost("/admin/workflow/{instanceId}/reset", (string instanceId, BusinessAppWorkflowEngine engine) =>
{
    engine.Reset(instanceId);
    return Results.Redirect("/admin/workflow");
});

app.MapPost("/admin/workflow/reset-all", (BusinessAppWorkflowEngine engine) =>
{
    engine.ResetAll();
    return Results.Redirect("/admin/workflow");
});

app.Run();

static string GetCallerTraceId(HttpRequest request)
{
    if (request.Headers.TryGetValue("X-Prism-Caller-TraceId", out var values))
    {
        var callerTraceId = values.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(callerTraceId))
        {
            return callerTraceId;
        }
    }

    return "absent";
}

public record BackOfficeMember(string Email, string TenantCode, string BackOfficeId, string Role);

public record WorkflowCurrentApiRequest(
    string? InstanceId,
    string? Action);

public record WorkflowAdvanceApiRequest(
    string InstanceId,
    string Action,
    int StateVersion,
    Dictionary<string, object?>? FieldValues);

// Passthrough sanitizer: seed content is developer-authored, not user-supplied.
// No XSS risk — passthrough is intentional and appropriate for this mock app.
file sealed class PassthroughSanitizer : IWorkflowContentSanitizer
{
    public string Sanitize(string? html) => html ?? string.Empty;
}
