using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using UmbracoPrism.Core.Extensions;
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.MockBusinessApp.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPrismAuthentication(builder.Configuration);

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

// Business App workflow engine — singleton so in-memory instance state survives across requests
builder.Services.AddSingleton<BusinessAppWorkflowEngine>();
builder.Services.AddHostedService<WorkflowTuiService>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();


app.MapGet("/api/backoffice/me", (IConfiguration config, ClaimsPrincipal user) =>
{

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
app.MapPost("/api/workflow/{workflowKey}/current", (
    string workflowKey,
    ClaimsPrincipal user,
    IConfiguration config,
    BusinessAppWorkflowEngine engine,
    ILogger<Program> logger) =>
{
    var tenant = user.GetPrismTenant(PrismResolvers.FromConfig(config));
    var email = user.GetEmail();

    if (tenant == null || string.IsNullOrEmpty(email))
        return Results.Unauthorized();

    logger.LogInformation("Workflow current: key={Key} tenant={Tenant} user={User}", workflowKey, tenant.Code, email);

    var envelope = engine.GetCurrent(workflowKey, tenant.Code, email);
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

app.MapDelete("/api/test/reset", (BusinessAppWorkflowEngine engine, ILogger<Program> logger) =>
{
    engine.ResetAll();
    logger.LogInformation("Test reset: all workflow instances cleared via /api/test/reset");
    return Results.Ok(new { cleared = true });
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
            var icon = s.StepType switch
            {
                "confirmation"    => " ✓",
                "status-timeline" => " ⏱",
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
                var icon = s.StepType switch
                {
                    "confirmation"    => "✓",
                    "status-timeline" => "⏱",
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

            return $"""
            <div class="def-card">
              <div class="def-header">
                <div>
                  <strong>{Esc(def.DisplayName)}</strong>
                  <span style="color:#888;font-size:.82rem;margin-left:.5rem">({Esc(def.DefinitionKey)} v{def.Version})</span>
                </div>
                <div style="display:flex;gap:.5rem;align-items:center">{policyBadge}</div>
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

public record BackOfficeMember(string Email, string TenantCode, string BackOfficeId, string Role);

public record WorkflowAdvanceApiRequest(
    string InstanceId,
    string Action,
    int StateVersion,
    Dictionary<string, object?>? FieldValues);