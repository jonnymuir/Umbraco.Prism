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

    static string Esc(string s) => System.Net.WebUtility.HtmlEncode(s);

    var rows = instances.Count == 0
        ? "<tr><td colspan=\"7\" style=\"text-align:center;color:#888;padding:1.5rem\">No workflow instances</td></tr>"
        : string.Join("\n", instances.Select((i, n) => $"""
            <tr>
              <td>{n + 1}</td>
              <td style="font-family:monospace;font-size:.8em">{Esc(i.InstanceId)}</td>
              <td>{Esc(i.WorkflowKey)}</td>
              <td><span class="badge">{Esc(i.CurrentState)}</span></td>
              <td>{Esc(i.TenantId)}</td>
              <td>{Esc(i.UserId)}</td>
              <td class="actions">
                <form method="post" action="/admin/workflow/{Esc(i.InstanceId)}/approve" style="display:inline">
                  <button class="btn btn-approve">Approve</button>
                </form>
                <form method="post" action="/admin/workflow/{Esc(i.InstanceId)}/reject" style="display:inline">
                  <button class="btn btn-reject">Reject</button>
                </form>
                <form method="post" action="/admin/workflow/{Esc(i.InstanceId)}/reset" style="display:inline">
                  <button class="btn btn-reset" onclick="return confirm('Remove this instance?')">Reset</button>
                </form>
              </td>
            </tr>
            """));

    var defRows = defs.Count == 0
        ? "<tr><td colspan=\"4\" style=\"text-align:center;color:#888\">No definitions loaded</td></tr>"
        : string.Join("\n", defs.Select(d => $"""
            <tr>
              <td>{Esc(d.DefinitionKey)}</td>
              <td>{Esc(d.DisplayName)}</td>
              <td>{d.States.Count}</td>
              <td>{d.Transitions.Count}</td>
            </tr>
            """));

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
            main { padding: 1.5rem; max-width: 1200px; margin: 0 auto; }
            h2 { font-size: .95rem; text-transform: uppercase; letter-spacing:.05em; color:#555; margin-top:2rem; }
            table { width:100%; border-collapse:collapse; background:#fff; border-radius:8px; overflow:hidden; box-shadow:0 1px 3px rgba(0,0,0,.08); }
            th { background:#f0f1f4; text-align:left; padding:.6rem .9rem; font-size:.8rem; text-transform:uppercase; letter-spacing:.04em; color:#555; }
            td { padding:.6rem .9rem; border-top:1px solid #eee; font-size:.88rem; vertical-align:middle; }
            tr:hover td { background:#fafbff; }
            .badge { background:#dbeafe; color:#1d4ed8; padding:.15rem .5rem; border-radius:999px; font-size:.78rem; font-weight:600; }
            .actions { white-space:nowrap; }
            .btn { border:none; border-radius:5px; padding:.25rem .65rem; font-size:.8rem; cursor:pointer; font-weight:600; }
            .btn-approve { background:#d1fae5; color:#065f46; }
            .btn-approve:hover { background:#a7f3d0; }
            .btn-reject  { background:#fee2e2; color:#991b1b; }
            .btn-reject:hover  { background:#fca5a5; }
            .btn-reset   { background:#fef3c7; color:#92400e; }
            .btn-reset:hover   { background:#fde68a; }
            .btn-reset-all { background:#fee2e2; color:#991b1b; padding:.35rem 1rem; }
            .btn-reset-all:hover { background:#fca5a5; }
            .toolbar { margin-bottom:.75rem; display:flex; gap:.5rem; align-items:center; }
            .count { color:#888; font-size:.85rem; }
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
            <table>
              <thead>
                <tr><th>Key</th><th>Name</th><th>States</th><th>Transitions</th></tr>
              </thead>
              <tbody>{{defRows}}</tbody>
            </table>
          </main>
        </body>
        </html>
        """;

    return Results.Content(html, "text/html");
});

app.MapPost("/admin/workflow/{instanceId}/approve", (string instanceId, BusinessAppWorkflowEngine engine) =>
{
    var envelope = engine.AdvanceAsReviewer(instanceId, "approve");
    return Results.Redirect("/admin/workflow");
});

app.MapPost("/admin/workflow/{instanceId}/reject", (string instanceId, BusinessAppWorkflowEngine engine) =>
{
    var envelope = engine.AdvanceAsReviewer(instanceId, "reject");
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