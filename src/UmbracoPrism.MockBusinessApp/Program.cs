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
using UmbracoPrism.WorkflowEditor.Authoring;
using UmbracoPrism.WorkflowEditor.Extensions;
using UmbracoPrism.WorkflowRuntime.Abstractions;
using UmbracoPrism.WorkflowRuntime.Extensions;

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

// Slice B: the editor library no longer ships an authored-workflow store. Each host
// owns its own persistence. The reference app keeps the four demo workflows in memory
// and exposes them through `/mockapp/workflows/*` for the editor's TS WorkflowSource.
builder.Services.AddSingleton<ReferenceAuthoredWorkflowStore>();
builder.Services.AddSingleton<IPublishedWorkflowStore, InMemoryRuntimePublishedWorkflowStore>();

// Editor library — projector / patch / simulation / action catalog only.
builder.Services.AddPrismWorkflowEditor();
// Publish service moved into MockBusinessApp in Slice B (it was always host-policy code).
builder.Services.AddSingleton<IWorkflowPublishService, WorkflowPublishService>();
builder.Services.AddBusinessAppWorkflowActions();

// Business App workflow engine — singleton so in-memory instance state survives across requests.
// The reference app uses ReferenceWorkflowDefinitionStore to seed exactly 4 workflows at runtime.
// Downstream apps can use FilesystemWorkflowDefinitionStore or their own IWorkflowDefinitionStore.
builder.Services.AddSingleton<IWorkflowDefinitionStore, ReferenceWorkflowDefinitionStore>();
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
};

app.MapGet("/mockapp/workflows", (ReferenceAuthoredWorkflowStore store) =>
    Results.Json(store.List(), mockWorkflowJsonOptions));

app.MapGet("/mockapp/workflows/{key}", (string key, ReferenceAuthoredWorkflowStore store) =>
{
    if (!System.Text.RegularExpressions.Regex.IsMatch(key, @"^[a-zA-Z0-9_\-]+$"))
    {
        return Results.Problem(
            detail: $"Workflow key '{key}' contains characters that are not allowed.",
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid workflow key");
    }
    var workflow = store.Load(key);
    return workflow is null
        ? Results.NotFound()
        : Results.Json(workflow, mockWorkflowJsonOptions);
});

app.MapPut("/mockapp/workflows/{key}", async (string key, HttpContext ctx, ReferenceAuthoredWorkflowStore store) =>
{
    if (!System.Text.RegularExpressions.Regex.IsMatch(key, @"^[a-zA-Z0-9_\-]+$"))
    {
        return Results.Problem(
            detail: $"Workflow key '{key}' contains characters that are not allowed.",
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid workflow key");
    }

    AuthoredWorkflow? workflow;
    try
    {
        workflow = await JsonSerializer.DeserializeAsync<AuthoredWorkflow>(
            ctx.Request.Body, mockWorkflowJsonOptions, ctx.RequestAborted);
    }
    catch (JsonException ex)
    {
        return Results.Problem(
            detail: ex.Message,
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid workflow JSON");
    }

    if (workflow is null)
    {
        return Results.Problem(
            detail: "Request body was empty.",
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid workflow JSON");
    }

    store.Save(key, workflow);
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

    var envelope = engine.GetCurrent(workflowKey, tenant.Code, email, instanceId, action);
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
        request.InstanceId, tenant.Code, email,
        request.Action, request.StateVersion, request.FieldValues);

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

app.MapGet("/admin/workflow", (BusinessAppWorkflowEngine engine, ReferenceAuthoredWorkflowStore authoredWorkflowStore) =>
{
    var instances = engine.GetAllInstances().OrderBy(i => i.CreatedAt).ToList();
    var defs = engine.GetAllDefinitions().ToList();
    var defsByKey = defs.ToDictionary(d => d.DefinitionKey, StringComparer.OrdinalIgnoreCase);
    var authoredWorkflowEntries = authoredWorkflowStore.List();
    var authoredWorkflowRouteKeysByDefinitionKey = authoredWorkflowEntries
        .Where(entry => !string.IsNullOrWhiteSpace(entry.DefinitionKey))
        .GroupBy(entry => entry.DefinitionKey, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.First().WorkflowKey, StringComparer.OrdinalIgnoreCase);
    var loadableAuthoredWorkflowKeys = authoredWorkflowEntries
        .Select(entry => entry.WorkflowKey)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    var invalidAuthoredWorkflowKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    string Esc(string s) => System.Net.WebUtility.HtmlEncode(s);
    string SafeId(string key) => key.Replace("-", "_").Replace(".", "_").Replace(" ", "_");
    string MermaidLabel(string s) => s.Replace("\"", "'");

    string ActionDisplay(string action) => action switch
    {
        "approve"          => "✓ Approve",
        "reject"           => "✗ Reject",
        "request-changes"  => "↩ Request Changes",
        "save-draft"       => "💾 Save Draft",
        "submit"           => "→ Submit",
        _ => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(action.Replace("-", " "))
    };

    string ActionBtnClass(string action) => action switch
    {
        "approve"         => "btn-approve",
        "reject"          => "btn-reject",
        "request-changes" => "btn-warn",
        _                 => "btn-action"
    };

    string BuildMermaid(WorkflowDefinitionFile def)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("stateDiagram-v2");
        sb.AppendLine("    direction LR");
        sb.AppendLine($"    [*] --> {SafeId(def.InitialState)}");
        foreach (var s in def.States)
        {
            var icon = s.Components.InferStepType() switch
            {
                "confirmation"    => " ✓",
                "status-timeline" => " ⏱",
                "waiting"         => " ⏳",
                "check-answers"   => " ✎",
                "task-list"       => " ☑",
                _                 => ""
            };
            sb.AppendLine($"    state \"{MermaidLabel(s.DisplayName)}{icon}\" as {SafeId(s.StateKey)}");
        }
        foreach (var t in def.Transitions)
        {
            var roleNote = string.Equals(t.RequiresRole, "reviewer", StringComparison.OrdinalIgnoreCase) ? " 🔒" : "";
            sb.AppendLine($"    {SafeId(t.FromState)} --> {SafeId(t.ToState)} : {t.Action}{roleNote}");
        }
        var statesWithOutgoing = def.Transitions
            .Select(t => t.FromState)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var s in def.States)
        {
            if (!statesWithOutgoing.Contains(s.StateKey))
                sb.AppendLine($"    {SafeId(s.StateKey)} --> [*]");
        }
        return sb.ToString();
    }

    var rows = instances.Count == 0
        ? """<tr><td colspan="7" style="text-align:center;color:#888;padding:1.5rem">No workflow instances</td></tr>"""
        : string.Join("\n", instances.Select((inst, n) =>
        {
            defsByKey.TryGetValue(inst.WorkflowKey, out var def);
            var stateDisplay = def?.States
                .FirstOrDefault(s => string.Equals(s.StateKey, inst.CurrentState, StringComparison.OrdinalIgnoreCase))
                ?.DisplayName ?? inst.CurrentState;
            var reviewerTransitions = def?.Transitions
                .Where(t =>
                    string.Equals(t.FromState, inst.CurrentState, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(t.RequiresRole, "reviewer", StringComparison.OrdinalIgnoreCase))
                .ToList() ?? [];
            var actionBtns = reviewerTransitions.Count == 0
                ? """<span style="color:#aaa;font-size:.78rem">—</span>"""
                : string.Join(" ", reviewerTransitions.Select(t => $"""
                    <form method="post" action="/admin/workflow/{Esc(inst.InstanceId)}/action/{Esc(t.Action)}" style="display:inline">
                      <button class="btn {ActionBtnClass(t.Action)}">{ActionDisplay(t.Action)}</button>
                    </form>
                    """));
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
              <td>{Esc(inst.UserId)}</td>
              <td class="actions">
                {actionBtns}
                <form method="post" action="/admin/workflow/{Esc(inst.InstanceId)}/reset" style="display:inline;margin-left:.25rem">
                  <button class="btn btn-reset" onclick="return confirm('Remove this instance?')">↺ Reset</button>
                </form>
              </td>
            </tr>
            """;
        }));

    var defCards = defs.Count == 0
        ? """<p style="color:#888;text-align:center">No definitions loaded</p>"""
        : string.Join("\n", defs.Select(def =>
        {
            var authoredWorkflowKey = authoredWorkflowRouteKeysByDefinitionKey.TryGetValue(def.DefinitionKey, out var resolvedWorkflowKey)
                ? resolvedWorkflowKey
                : loadableAuthoredWorkflowKeys.Contains(def.DefinitionKey)
                    ? def.DefinitionKey
                    : null;
            var hasAuthoredWorkflow = authoredWorkflowKey is not null;
            var stateRows = string.Join("\n", def.States.Select(s =>
            {
                var icon = s.Components.InferStepType() switch
                {
                    "confirmation"    => "✓",
                    "status-timeline" => "⏱",
                    "waiting"         => "⏳",
                    "check-answers"   => "✎",
                    "task-list"       => "☑",
                    "question"        => "✏",
                    _                 => "·"
                };
                var initBadge = string.Equals(s.StateKey, def.InitialState, StringComparison.OrdinalIgnoreCase)
                    ? """<span class="badge badge-initial">initial</span>"""
                    : "";
                return $"""
                <tr>
                  <td style="font-family:monospace;font-size:.8em">{Esc(s.StateKey)}</td>
                  <td>{Esc(s.DisplayName)}</td>
                  <td style="text-align:center">{icon}</td>
                  <td>{initBadge}</td>
                </tr>
                """;
            }));

            var transRows = string.Join("\n", def.Transitions.Select(t =>
            {
                var roleBadge = t.RequiresRole != null
                    ? $"""<span class="badge badge-role">🔒 {Esc(t.RequiresRole)}</span>"""
                    : """<span class="badge badge-any">any user</span>""";
                return $"""
                <tr>
                  <td style="font-family:monospace;font-size:.8em">{Esc(t.FromState)}</td>
                  <td style="font-family:monospace;font-size:.8em">{Esc(t.ToState)}</td>
                  <td><code>{Esc(t.Action)}</code></td>
                  <td>{roleBadge}</td>
                </tr>
                """;
            }));

            var mermaidText = BuildMermaid(def);
            var policyBadge = def.InstancePolicy switch
            {
                "single"   => """<span class="badge badge-policy">single instance</span>""",
                "multiple" => """<span class="badge badge-policy-m">multiple instances</span>""",
                _          => $"""<span class="badge badge-policy">{Esc(def.InstancePolicy)}</span>"""
            };

            // V2: field groups removed — components are inline in each state's component tree.
            var fieldGroupsSection = "";
            var editorShortcut = hasAuthoredWorkflow
                ? $"""<a class="btn btn-edit-workflow" href="/workflow-editor?workflow={Esc(authoredWorkflowKey!)}">↗ Edit workflow</a>"""
                : invalidAuthoredWorkflowKeys.Contains(def.DefinitionKey)
                  ? "<span class=\"editor-unavailable\""
                    + " aria-label=\"Workflow editor unavailable because this workflow's editor definition is invalid and needs repair.\""
                    + " title=\"Repair the editor definition before opening it in the reference editor.\">Editor definition invalid</span>"
                : "<span class=\"editor-unavailable\""
                  + " aria-label=\"Workflow editor unavailable because this workflow is not configured for the editor yet.\""
                  + " title=\"This workflow currently has no editor definition configured.\">No editor definition yet</span>";
            var editorJsonKey = authoredWorkflowKey ?? def.DefinitionKey;

            return $"""
            <div class="def-card"
                 data-workflow-key="{Esc(editorJsonKey)}"
                 data-definition-key="{Esc(def.DefinitionKey)}"
                 data-mermaid-render-state="idle">
              <div class="def-header"
                   role="button"
                   tabindex="0"
                   aria-expanded="false"
                   onclick="toggleCard(event, this)"
                   onkeydown="handleCardHeaderKeydown(event, this)">
                <div style="display:flex;align-items:center;gap:.5rem">
                  <span class="def-toggle">▶</span>
                  <div>
                    <strong>{Esc(def.DisplayName)}</strong>
                    <span style="color:#888;font-size:.82rem;margin-left:.5rem">({Esc(def.DefinitionKey)} v{def.Version})</span>
                  </div>
                </div>
                <div class="def-actions">
                  {policyBadge}
                  {editorShortcut}
                </div>
              </div>
              <div class="def-body">
                <div class="def-tables">
                  <div>
                    <p class="table-label">States ({def.States.Count})</p>
                    <table>
                      <thead><tr><th>Key</th><th>Display Name</th><th>Type</th><th></th></tr></thead>
                      <tbody>{stateRows}</tbody>
                    </table>
                  </div>
                  <div>
                    <p class="table-label">Transitions ({def.Transitions.Count})</p>
                    <table>
                      <thead><tr><th>From</th><th>To</th><th>Action</th><th>Role</th></tr></thead>
                      <tbody>{transRows}</tbody>
                    </table>
                  </div>
                </div>
                {fieldGroupsSection}
                <div class="def-diagram">
                  <p class="table-label">State Diagram</p>
                  <div class="mermaid">{Esc(mermaidText)}</div>
                </div>
              </div>
            </div>
            """;
        }));

    var html = $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8"/>
          <meta name="viewport" content="width=device-width,initial-scale=1"/>
          <title>Workflow Admin — MockBusinessApp</title>
          <script type="module">
            import mermaid from 'https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.esm.min.mjs';
            mermaid.initialize({ startOnLoad: false, theme: 'neutral', securityLevel: 'loose' });
            window._mermaid = mermaid;
            window._mermaidReady = true;
            window.dispatchEvent(new CustomEvent('prism:mermaid-ready'));
          </script>
          <style>
            *, *::before, *::after { box-sizing: border-box; }
            body { font-family: system-ui, sans-serif; margin: 0; background: #f4f5f7; color: #1a1a2e; }
            header { background: #1a1a2e; color: #fff; padding: 1rem 1.5rem; display:flex; align-items:center; gap:1rem; }
            header h1 { margin:0; font-size:1.1rem; }
            main { padding: 1.5rem; max-width: 1300px; margin: 0 auto; }
            h2 { font-size: .95rem; text-transform: uppercase; letter-spacing:.05em; color:#555; margin-top:2rem; }
            table { width:100%; border-collapse:collapse; background:#fff; border-radius:8px; overflow:hidden; box-shadow:0 1px 3px rgba(0,0,0,.08); }
            th { background:#f0f1f4; text-align:left; padding:.6rem .9rem; font-size:.8rem; text-transform:uppercase; letter-spacing:.04em; color:#555; }
            td { padding:.6rem .9rem; border-top:1px solid #eee; font-size:.88rem; vertical-align:middle; }
            tr:hover td { background:#fafbff; }
            .badge { background:#dbeafe; color:#1d4ed8; padding:.15rem .5rem; border-radius:999px; font-size:.78rem; font-weight:600; }
            .badge-initial { background:#d1fae5; color:#065f46; }
            .badge-role { background:#fef3c7; color:#92400e; }
            .badge-any { background:#f3f4f6; color:#6b7280; }
            .badge-policy { background:#e0e7ff; color:#3730a3; }
            .badge-policy-m { background:#fce7f3; color:#9d174d; }
            .actions { white-space:nowrap; }
            .btn { border:none; border-radius:5px; padding:.25rem .65rem; font-size:.8rem; cursor:pointer; font-weight:600; }
            .btn-approve { background:#d1fae5; color:#065f46; }
            .btn-approve:hover { background:#a7f3d0; }
            .btn-reject  { background:#fee2e2; color:#991b1b; }
            .btn-reject:hover  { background:#fca5a5; }
            .btn-warn    { background:#fef3c7; color:#92400e; }
            .btn-warn:hover    { background:#fde68a; }
            .btn-action  { background:#dbeafe; color:#1e40af; }
            .btn-action:hover  { background:#bfdbfe; }
            .btn-reset   { background:#f3f4f6; color:#374151; }
            .btn-reset:hover   { background:#e5e7eb; }
            .btn-reset-all { background:#fee2e2; color:#991b1b; padding:.35rem 1rem; }
            .btn-reset-all:hover { background:#fca5a5; }
            .header-links { margin-left:auto; display:flex; gap:.5rem; flex-wrap:wrap; }
            .header-link {
              display:inline-flex;
              align-items:center;
              justify-content:center;
              padding:.45rem .8rem;
              border-radius:999px;
              background:rgba(255,255,255,.12);
              color:#fff;
              text-decoration:none;
              font-size:.82rem;
              font-weight:600;
            }
            .header-link:hover { background:rgba(255,255,255,.2); }
            .toolbar { margin-bottom:.75rem; display:flex; gap:.5rem; align-items:center; }
            .count { color:#888; font-size:.85rem; }
            .def-card { background:#fff; border-radius:8px; box-shadow:0 1px 3px rgba(0,0,0,.08); margin-bottom:1.25rem; overflow:hidden; }
            .def-header { padding:.75rem 1rem; background:#f8f9fb; border-bottom:1px solid #eee; display:flex; justify-content:space-between; align-items:center; cursor:pointer; }
            .def-header:focus-visible { outline:3px solid #2563eb; outline-offset:-3px; }
            .def-actions { display:flex; gap:.5rem; align-items:center; flex-wrap:wrap; justify-content:flex-end; }
            .def-toggle { display:inline-block; transition:transform .16s ease; }
            .def-card.open .def-toggle { transform:rotate(90deg); }
            .def-body { padding:1rem; display:none; flex-direction:column; gap:1rem; }
            .def-card.open > .def-body { display:flex; }
            .def-tables { display:grid; grid-template-columns:1fr 1fr; gap:1rem; }
            .def-diagram .mermaid { background:#fafafa; border-radius:6px; padding:.75rem 1rem; overflow-x:auto; }
            .table-label { margin:0 0 .4rem; font-size:.78rem; text-transform:uppercase; letter-spacing:.04em; color:#888; font-weight:600; }
            code { background:#f0f1f4; border-radius:3px; padding:.1rem .35rem; font-size:.82em; }
            .modal-overlay { position:fixed; inset:0; background:rgba(0,0,0,.55); display:flex; align-items:center; justify-content:center; z-index:1000; }
            .modal-box { background:#fff; border-radius:10px; width:min(900px,95vw); max-height:90vh; display:flex; flex-direction:column; box-shadow:0 8px 32px rgba(0,0,0,.25); }
            .modal-hdr { padding:.85rem 1.1rem; border-bottom:1px solid #eee; display:flex; justify-content:space-between; align-items:center; font-size:.95rem; }
            .modal-close { background:none; border:none; font-size:1.1rem; cursor:pointer; color:#666; padding:.15rem .4rem; }
            .modal-close:hover { color:#000; }
            .modal-ftr { padding:.75rem 1rem; border-top:1px solid #eee; display:flex; align-items:center; gap:.6rem; }
            .save-msg { flex:1; font-size:.82rem; color:#b91c1c; }
            .btn-edit-workflow {
              background:#dbeafe;
              color:#1d4ed8;
              text-decoration:none;
            }
            .btn-edit-workflow:hover { background:#bfdbfe; }
            .editor-unavailable {
              display:inline-flex;
              align-items:center;
              min-height:30px;
              padding:.25rem .65rem;
              border-radius:999px;
              background:#f3f4f6;
              color:#4b5563;
              font-size:.8rem;
              font-weight:600;
            }
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
                  <th>#</th><th>Instance ID</th><th>Workflow</th><th>State</th><th>Tenant</th><th>User</th><th>Actions</th>
                </tr>
              </thead>
              <tbody>{{rows}}</tbody>
            </table>

            <h2>Workflow Definitions</h2>
            <div class="toolbar">
              <span class="count">{{defs.Count}} definition(s)</span>
              <button type="button" class="btn btn-action" onclick="expandAllDefs()">Expand All</button>
              <button type="button" class="btn btn-reset" onclick="collapseAllDefs()">Collapse All</button>
            </div>
            {{defCards}}
          </main>

          <script>
            function setCardOpen(card, isOpen) {
              if (!card) return;
              card.classList.toggle('open', isOpen);
              const header = card.querySelector('.def-header');
              if (header) header.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
              if (isOpen) renderCardDiagram(card);
            }

            function toggleCard(e, hdr) {
              if (e.target instanceof Element && e.target.closest('button, a')) return;
              const card = hdr.closest('.def-card');
              setCardOpen(card, !card.classList.contains('open'));
            }

            function handleCardHeaderKeydown(e, hdr) {
              if (e.key !== 'Enter' && e.key !== ' ') return;
              e.preventDefault();
              toggleCard(e, hdr);
            }

            async function renderCardDiagram(card) {
              if (!window._mermaid) return;
              const nodes = Array.from(card.querySelectorAll('.mermaid:not([data-processed])'));
              if (!nodes.length) {
                card.setAttribute('data-mermaid-render-state', 'ready');
                return;
              }

              card.setAttribute('data-mermaid-render-state', 'rendering');

              try {
                await window._mermaid.run({ nodes });
                card.setAttribute('data-mermaid-render-state', 'ready');
              } catch (error) {
                card.setAttribute('data-mermaid-render-state', 'error');
                console.error('Mermaid rendering failed for workflow admin card.', error);
              }
            }

            function renderOpenCardDiagrams() {
              document.querySelectorAll('.def-card.open').forEach(card => renderCardDiagram(card));
            }

            if (window._mermaidReady) {
              renderOpenCardDiagrams();
            } else {
              window.addEventListener('prism:mermaid-ready', renderOpenCardDiagrams, { once: true });
            }

            function expandAllDefs() {
              document.querySelectorAll('.def-card').forEach(c => setCardOpen(c, true));
            }

            function collapseAllDefs() {
              document.querySelectorAll('.def-card').forEach(c => setCardOpen(c, false));
            }
          </script>
        </body>
        </html>
        """;

    return Results.Content(html, "text/html");
});

app.MapPost("/admin/workflow/{instanceId}/action/{action}", (string instanceId, string action, BusinessAppWorkflowEngine engine) =>
{
    engine.AdvanceAsReviewer(instanceId, action);
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
