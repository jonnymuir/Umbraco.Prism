using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using UmbracoPrism.Core.Extensions;
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.MockBusinessApp.Services;
using UmbracoPrism.Shared.Extensions;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.Shared.Services.Sanitization;

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

// Business App workflow engine — singleton so in-memory instance state survives across requests
builder.Services.AddSingleton<BusinessAppWorkflowEngine>();
builder.Services.AddHostedService<WorkflowTuiService>();

var app = builder.Build();

// SECURITY: KEYCLOAK_BACKCHANNEL_URL must never be set in production — it bypasses
// TLS certificate validation for OIDC metadata fetches, which is only acceptable
// in controlled development environments. Fail loudly if misconfigured.
if (!app.Environment.IsDevelopment() && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KEYCLOAK_BACKCHANNEL_URL")))
{
    throw new InvalidOperationException("KEYCLOAK_BACKCHANNEL_URL must not be set in non-Development environments.");
}

// SECURITY: Admin workflow endpoints should not exist outside Development mode.
// Defence-in-depth: ensure they're unreachable even if accidentally deployed.
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

    if (tenant == null || string.IsNullOrEmpty(email))
        return Results.Unauthorized();

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

    if (tenant == null || string.IsNullOrEmpty(email))
        return Results.Unauthorized();

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

    if (tenant == null || string.IsNullOrEmpty(email))
        return Results.Unauthorized();

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

app.MapGet("/admin/workflow", (BusinessAppWorkflowEngine engine) =>
{
    var instances = engine.GetAllInstances().OrderBy(i => i.CreatedAt).ToList();
    var defs = engine.GetAllDefinitions().ToList();
    var defsByKey = defs.ToDictionary(d => d.DefinitionKey, StringComparer.OrdinalIgnoreCase);

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
            return $"""
            <tr>
              <td>{n + 1}</td>
              <td style="font-family:monospace;font-size:.8em">{Esc(inst.InstanceId)}</td>
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

            return $"""
            <div class="def-card">
              <div class="def-header">
                <div>
                  <strong>{Esc(def.DisplayName)}</strong>
                  <span style="color:#888;font-size:.82rem;margin-left:.5rem">({Esc(def.DefinitionKey)} v{def.Version})</span>
                </div>
                <div style="display:flex;gap:.5rem;align-items:center">
                  {policyBadge}
                  <button class="btn btn-edit" onclick="openEditor('{Esc(def.DefinitionKey)}')">✎ Edit JSON</button>
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
          <script src="https://cdnjs.cloudflare.com/ajax/libs/ace/1.32.6/ace.min.js"></script>
          <script type="module">
            import mermaid from 'https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.esm.min.mjs';
            mermaid.initialize({ startOnLoad: true, theme: 'neutral', securityLevel: 'loose' });
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
            .toolbar { margin-bottom:.75rem; display:flex; gap:.5rem; align-items:center; }
            .count { color:#888; font-size:.85rem; }
            .def-card { background:#fff; border-radius:8px; box-shadow:0 1px 3px rgba(0,0,0,.08); margin-bottom:1.25rem; overflow:hidden; }
            .def-header { padding:.75rem 1rem; background:#f8f9fb; border-bottom:1px solid #eee; display:flex; justify-content:space-between; align-items:center; }
            .def-body { padding:1rem; display:flex; flex-direction:column; gap:1rem; }
            .def-tables { display:grid; grid-template-columns:1fr 1fr; gap:1rem; }
            .def-diagram .mermaid { background:#fafafa; border-radius:6px; padding:.75rem 1rem; overflow-x:auto; }
            .table-label { margin:0 0 .4rem; font-size:.78rem; text-transform:uppercase; letter-spacing:.04em; color:#888; font-weight:600; }
            code { background:#f0f1f4; border-radius:3px; padding:.1rem .35rem; font-size:.82em; }
            .modal-overlay { position:fixed; inset:0; background:rgba(0,0,0,.55); display:flex; align-items:center; justify-content:center; z-index:1000; }
            .modal-box { background:#fff; border-radius:10px; width:min(900px,95vw); max-height:90vh; display:flex; flex-direction:column; box-shadow:0 8px 32px rgba(0,0,0,.25); }
            .modal-hdr { padding:.85rem 1.1rem; border-bottom:1px solid #eee; display:flex; justify-content:space-between; align-items:center; font-size:.95rem; }
            .modal-close { background:none; border:none; font-size:1.1rem; cursor:pointer; color:#666; padding:.15rem .4rem; }
            .modal-close:hover { color:#000; }
            #ace-editor { flex:1; min-height:400px; font-size:13px; }
            .modal-ftr { padding:.75rem 1rem; border-top:1px solid #eee; display:flex; align-items:center; gap:.6rem; }
            .save-msg { flex:1; font-size:.82rem; color:#b91c1c; }
            .btn-edit { background:#f0f4ff; color:#3730a3; margin-left:.5rem; }
            .btn-edit:hover { background:#e0e7ff; }
          </style>
        </head>
        <body>
          <header>
            <h1>🔧 Workflow Admin</h1>
            <span style="opacity:.6;font-size:.85rem">MockBusinessApp — local dev only</span>
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
            {{defCards}}
          </main>
          
          <div id="json-modal" class="modal-overlay" style="display:none" onclick="handleOverlayClick(event)">
            <div class="modal-box">
              <div class="modal-hdr">
                <span id="modal-title">✎ Edit — <strong id="modal-key"></strong></span>
                <button class="modal-close" onclick="closeEditor()">✕</button>
              </div>
              <div id="ace-editor"></div>
              <div class="modal-ftr">
                <span id="save-msg" class="save-msg"></span>
                <button class="btn btn-reset" onclick="closeEditor()">Cancel</button>
                <button class="btn btn-approve" id="apply-btn" onclick="saveDefinition()">Apply Changes</button>
              </div>
            </div>
          </div>
          
          <script>
            let aceEditor = null;
            let currentEditorKey = null;
            let currentEditorType = null;

            async function openEditor(key) {
              currentEditorKey = key;
              currentEditorType = 'definition';
              document.getElementById('modal-title').innerHTML = '✎ Edit Workflow Definition — <strong id="modal-key">' + key + '</strong>';
              document.getElementById('modal-key').textContent = key;
              document.getElementById('save-msg').textContent = '';
              document.getElementById('json-modal').style.display = 'flex';
              
              const res = await fetch('/admin/workflow/definition/' + encodeURIComponent(key) + '/json');
              const json = await res.text();
              
              if (!aceEditor) {
                aceEditor = ace.edit('ace-editor');
                aceEditor.setTheme('ace/theme/tomorrow');
                aceEditor.session.setMode('ace/mode/json');
                aceEditor.setOptions({ fontSize: '13px', tabSize: 2, useSoftTabs: true, showPrintMargin: false });
              }
              aceEditor.setValue(json, -1);
            }

            function closeEditor() {
              document.getElementById('json-modal').style.display = 'none';
            }

            function handleOverlayClick(e) {
              if (e.target === document.getElementById('json-modal')) closeEditor();
            }

            async function saveDefinition() {
              const json = aceEditor.getValue();
              try { JSON.parse(json); } catch(e) {
                document.getElementById('save-msg').textContent = '⚠ Invalid JSON: ' + e.message;
                return;
              }
              
              document.getElementById('apply-btn').disabled = true;
              document.getElementById('save-msg').textContent = 'Saving…';
              
              const endpoint = '/admin/workflow/definition/' + encodeURIComponent(currentEditorKey);
              
              const res = await fetch(endpoint, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: json
              });
              
              if (res.ok) {
                closeEditor();
                window.location.reload();
              } else {
                const text = await res.text();
                document.getElementById('save-msg').textContent = '⚠ ' + text;
                document.getElementById('apply-btn').disabled = false;
              }
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

app.MapGet("/admin/workflow/definition/{key}/json", (string key, BusinessAppWorkflowEngine engine) =>
{
    // SECURITY: Validate key format to prevent path traversal or injection
    if (!System.Text.RegularExpressions.Regex.IsMatch(key, @"^[a-zA-Z0-9\-]+$"))
        return Results.BadRequest("Invalid workflow key.");
    
    var def = engine.GetDefinition(key);
    if (def == null) return Results.NotFound();
    
    var opts = new System.Text.Json.JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
    var json = System.Text.Json.JsonSerializer.Serialize(def, opts);
    return Results.Content(json, "application/json");
});

app.MapPut("/admin/workflow/definition/{key}", async (string key, HttpContext ctx, BusinessAppWorkflowEngine engine) =>
{
    // SECURITY: Validate key format to prevent path traversal or injection
    if (!System.Text.RegularExpressions.Regex.IsMatch(key, @"^[a-zA-Z0-9\-]+$"))
        return Results.BadRequest("Invalid workflow key.");
    
    WorkflowDefinitionFile? updated;
    try
    {
        updated = await System.Text.Json.JsonSerializer.DeserializeAsync<WorkflowDefinitionFile>(
            ctx.Request.Body,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
    catch (System.Text.Json.JsonException ex)
    {
        return Results.BadRequest($"Invalid JSON: {ex.Message}");
    }
    if (updated == null) return Results.BadRequest("Empty body");
    var success = engine.UpdateDefinition(key, updated);
    return success ? Results.Ok(new { updated = key }) : Results.NotFound();
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
