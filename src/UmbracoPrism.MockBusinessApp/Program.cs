using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using UmbracoPrism.Core.Extensions;
using UmbracoPrism.MockBusinessApp.Services;
using UmbracoPrism.MockBusinessApp.Services.Actions;
using Wayfinder.Extensions;
using Wayfinder.Models.ServiceDesign;
using Wayfinder.Models.ServiceDesign.Components;
using Wayfinder.Services.Sanitization;
using Wayfinder.Engine.Abstractions;
using Wayfinder.Engine.Api;
using Wayfinder.Engine.Extensions;
using Wayfinder.Engine.Mcp;
using Wayfinder.Engine.Services;

var builder = WebApplication.CreateBuilder(args);

// Local secrets override — gitignored. Supply real Entra tenant/client IDs and member
// emails here. See src/UmbracoPrism.MockBusinessApp/README.md for setup instructions.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddPrismAuthentication(builder.Configuration);

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

// MockBusinessApp serves controlled seed content only — passthrough sanitizer is sufficient.
// The real GDS allowlist sanitizer (ServiceContentSanitizer) is wired up in TestSite via Core.
builder.Services.AddSingleton<IServiceContentSanitizer, PassthroughSanitizer>();

// The reference app keeps the demo service blueprints in memory. `/mockapp/service-blueprints/*` (the
// editor's own save endpoint) and the AI/tooling authoring surface below share this same
// IServiceBlueprintSourceStore, so a save from either surface is immediately visible to both
// (InMemoryRuntimePublishedServiceBlueprintStore.SaveAsync calls engine.UpdateDefinition). See
// MapPrismServiceBlueprintAuthoringApi()/MapPrismServiceBlueprintAuthoringMcp() below.
builder.Services.AddSingleton<IServiceBlueprintSourceStore, InMemoryRuntimePublishedServiceBlueprintStore>();
builder.Services.AddSingleton<IQueueCapabilitiesProvider>(ReferenceQueues.CapabilitiesProvider());
builder.Services.AddPrismServiceBlueprintAuthoring();
builder.Services.AddPrismServiceBlueprintAuthoringMcp();

builder.Services.AddBusinessAppActions();

// Business App process manager — singleton so in-memory instance state survives across requests.
// The reference app uses ReferenceServiceBlueprintStore to seed exactly 4 service blueprints at runtime.
// Downstream apps can use FilesystemServiceBlueprintStore or their own IServiceBlueprintStore.
builder.Services.AddSingleton<IServiceBlueprintStore, ReferenceServiceBlueprintStore>();
builder.Services.AddSingleton<UmbracoPrism.MockBusinessApp.Services.MoneyModeller.MemberRecordService>();
builder.Services.AddSingleton<BusinessAppProcessManager>();
builder.Services.AddSingleton<IProcessManager>(sp => sp.GetRequiredService<BusinessAppProcessManager>());

var app = builder.Build();

// Serve the Vite-built service-blueprint-editor.html (and its JS/CSS assets) from the ServiceBlueprintEditor wwwroot/dist
// output directory. This lets the walkthrough spec navigate to /service-blueprint-editor.html on this host.
var distPath = Path.GetFullPath(
    Path.Combine(builder.Environment.ContentRootPath, "..", "Wayfinder.Editor", "wwwroot", "dist"));
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

app.MapGet("/service-blueprint-editor", (HttpRequest request) =>
{
    var serviceBlueprintKey = request.Query["serviceBlueprint"].ToString();
    var targetServiceBlueprint = string.IsNullOrWhiteSpace(serviceBlueprintKey) ? "planning" : serviceBlueprintKey;

    return Results.Redirect($"/service-blueprint-editor.html?serviceBlueprint={Uri.EscapeDataString(targetServiceBlueprint)}");
});

// SECURITY: KEYCLOAK_BACKCHANNEL_URL must never be set in production — it bypasses
// TLS certificate validation for OIDC metadata fetches, which is only acceptable
// in controlled development environments. Fail loudly if misconfigured.
if (!app.Environment.IsDevelopment() && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KEYCLOAK_BACKCHANNEL_URL")))
{
    throw new InvalidOperationException("KEYCLOAK_BACKCHANNEL_URL must not be set in non-Development environments.");
}

// SECURITY: Admin service-desk endpoints should not exist outside Development mode in
// the reference app. Slice B retired the platform `/api/service-blueprint-authoring` API,
// so only `/admin` is gated here. The MockBusinessApp's `/mockapp/service-blueprints/*`
// endpoints are deliberately anonymous in the reference app — downstream hosts
// add whatever auth their TS ServiceBlueprintSource integration needs.
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

// Slice B: TS `ServiceBlueprintSource` HTTP integration. The reference app exposes the
// four demo service blueprints over /mockapp/service-blueprints/*. There is intentionally NO auth
// on these endpoints — the reference app proves the editor boundary works
// without inheriting authoring policies. Real downstream apps add their own
// authentication/authorization here (e.g. require an Entra group, a tenant
// claim, or a session cookie) before exposing this surface in production.
var mockServiceBlueprintJsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
    AllowOutOfOrderMetadataProperties = true,
};

// AI/tooling authoring API — list/read/validate/save/simulate service blueprint definitions against
// the live IServiceBlueprintSourceStore above. Intentionally NO auth here either, for the same
// reference-app reason as the block below: real downstream apps chain their own
// .RequireAuthorization() (or any other policy) onto the returned route group before
// exposing this to anything beyond localhost. Same story for the MCP endpoint — an AI
// agent (e.g. Claude Code via `claude mcp add --transport http`) calls the same
// ServiceBlueprintAuthoringService in-process, so a save reaches the live engine immediately.
app.MapPrismServiceBlueprintAuthoringApi();
app.MapPrismServiceBlueprintAuthoringMcp();

app.MapGet("/mockapp/service-blueprints", async (IServiceBlueprintSourceStore store, CancellationToken ct) =>
    Results.Json(await store.ListAsync(ct), mockServiceBlueprintJsonOptions));

app.MapGet("/mockapp/service-blueprints/{key}", async (string key, IServiceBlueprintSourceStore store, CancellationToken ct) =>
{
    if (!System.Text.RegularExpressions.Regex.IsMatch(key, @"^[a-zA-Z0-9_\-]+$"))
    {
        return Results.Problem(
            detail: $"Service blueprint key '{key}' contains characters that are not allowed.",
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid service blueprint key");
    }
    var serviceBlueprint = await store.LoadAsync(key, ct);
    return serviceBlueprint is null
        ? Results.NotFound()
        : Results.Json(serviceBlueprint, mockServiceBlueprintJsonOptions);
});

app.MapPut("/mockapp/service-blueprints/{key}", async (string key, HttpContext ctx, IServiceBlueprintSourceStore store, ServiceBlueprintAuthoringService authoringService, ILogger<Program> logger) =>
{
    if (!System.Text.RegularExpressions.Regex.IsMatch(key, @"^[a-zA-Z0-9_\-]+$"))
    {
        return Results.Problem(
            detail: $"Service blueprint key '{key}' contains characters that are not allowed.",
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid service blueprint key");
    }

    var parseResult = await ServiceBlueprintSourceSaveRequestParser.ParseAsync(ctx, mockServiceBlueprintJsonOptions, authoringService, ctx.RequestAborted);
    if (parseResult.Problem is not null)
    {
        return ServiceBlueprintSourceSaveRequestParser.ToProblemResult(ctx, parseResult.Problem);
    }

    var serviceBlueprint = parseResult.ServiceBlueprintValue!;

    if (!string.Equals(serviceBlueprint.DefinitionKey, key, StringComparison.Ordinal))
    {
        return Results.Problem(
            detail: $"Route key '{key}' does not match body definitionKey '{serviceBlueprint.DefinitionKey}'.",
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid service blueprint payload");
    }

    // InMemoryRuntimePublishedServiceBlueprintStore.SaveAsync already calls engine.UpdateDefinition —
    // no separate call needed here now that this shares the toolkit's IServiceBlueprintSourceStore.
    // ServiceBlueprintSourceSaveRequestParser already validated above, so this calls the store
    // directly rather than ServiceBlueprintAuthoringService.SaveAsync (which would just re-validate).
    // serviceBlueprint.Version — round-tripped by any client that loaded this service blueprint first — is the
    // optimistic-concurrency expected version; see IServiceBlueprintSourceStore.SaveAsync.
    var saveResult = await store.SaveAsync(serviceBlueprint, serviceBlueprint.Version, ctx.RequestAborted);
    if (!saveResult.Saved)
    {
        // Same ServiceBlueprintSaveOutcome shape the /prism/service-blueprint-authoring/* PUT returns on
        // conflict, so a client only needs one parser regardless of which endpoint it used.
        return Results.Conflict(ServiceBlueprintSaveOutcome.Conflict(saveResult.CurrentVersion));
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

// Service request API — server-to-server calls from Umbraco TestSite forwarding the member's Bearer token.
// Identity is derived from JWT claims; never trusted from the request body.
app.MapPost("/api/service-request/{blueprintKey}/current", async (
    string blueprintKey,
    ClaimsPrincipal user,
    IConfiguration config,
    BusinessAppProcessManager engine,
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
        var body = await context.Request.ReadFromJsonAsync<ServiceRequestCurrentApiRequest>();
        instanceId = body?.InstanceId;
        action = body?.Action;
    }
    catch
    {
        // Body is optional; empty/null body is fine
    }

    logger.LogInformation("Service request current: key={Key} tenant={Tenant} user={User} instanceId={InstanceId} action={Action}", 
        blueprintKey, tenant.Code, email, instanceId ?? "(none)", action ?? "(none)");

    var envelope = engine.GetCurrent(
        blueprintKey,
        tenant.Code,
        email,
        ReferenceQueues.WebUserProfile(),
        instanceId,
        action);
    return envelope.ResponseState == "error" ? Results.UnprocessableEntity(envelope) : Results.Ok(envelope);
}).RequireAuthorization();

app.MapPost("/api/service-request/{blueprintKey}/advance", (
    string blueprintKey,
    ServiceRequestAdvanceApiRequest request,
    ClaimsPrincipal user,
    IConfiguration config,
    BusinessAppProcessManager engine,
    ILogger<Program> logger) =>
{
    var tenant = user.GetPrismTenant(PrismResolvers.FromConfig(config));
    var email = user.GetEmail();

    if (tenant == null)
        return Results.Problem("Tenant not recognised by Business Application.");
    if (string.IsNullOrEmpty(email))
        return Results.Problem("User email claim not found.");

    logger.LogInformation(
        "Service request advance: key={Key} instance={Instance} action={Action}",
        blueprintKey, request.InstanceId, request.Action);

    var envelope = engine.Advance(
        request.InstanceId,
        tenant.Code,
        email,
        ReferenceQueues.WebUserProfile(),
        request.Action,
        request.StateVersion,
        request.FieldValues);

    return envelope.ResponseState == "error" ? Results.UnprocessableEntity(envelope) : Results.Ok(envelope);
}).RequireAuthorization();

app.MapGet("/api/service-request/instances", (
    ClaimsPrincipal user,
    IConfiguration config,
    BusinessAppProcessManager engine,
    ILogger<Program> logger) =>
{
    var tenant = user.GetPrismTenant(PrismResolvers.FromConfig(config));
    var email = user.GetEmail();

    if (tenant == null)
        return Results.Problem("Tenant not recognised by Business Application.");
    if (string.IsNullOrEmpty(email))
        return Results.Problem("User email claim not found.");

    logger.LogInformation("Service request instances: tenant={Tenant} user={User}", tenant.Code, email);

    var envelope = engine.GetInstances(tenant.Code, email);
    return Results.Ok(envelope);
}).RequireAuthorization();

// SECURITY: Anonymous test-reset endpoint — Development only.
// This endpoint wipes all service request instances and is intended exclusively for
// integration test setup/teardown. It MUST NOT be reachable in any non-Development
// environment (the global /admin guard above does not match this path, so guard
// the endpoint explicitly here).
app.MapDelete("/api/test/reset", (BusinessAppProcessManager engine, ILogger<Program> logger, IHostEnvironment env) =>
{
    if (!env.IsDevelopment())
    {
        return Results.NotFound();
    }

    engine.ResetAll();
    logger.LogInformation("Test reset: all service request instances cleared via /api/test/reset");
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

app.MapGet("/admin/service-desk", async (BusinessAppProcessManager engine, IServiceBlueprintSourceStore serviceBlueprintSourceStore) =>
{
    var instances = engine.GetAllInstances().OrderBy(i => i.CreatedAt).ToList();
    var businessQueue = engine.GetQueueWorkItems(ReferenceQueues.BusinessUserProfile()).Items;
    var defs = engine.GetAllDefinitions().ToList();
    var defsByKey = defs.ToDictionary(d => d.DefinitionKey, StringComparer.OrdinalIgnoreCase);
    // ServiceBlueprintSourceSummary is keyed by definitionKey alone (no separate route/blueprint key
    // concept — confirmed identical for every reference seed), so this no longer needs the
    // blueprintKey-vs-definitionKey bridging the old reference store's summary type required.
    var sourceServiceBlueprintDefinitionKeys = (await serviceBlueprintSourceStore.ListAsync())
        .Select(entry => entry.DefinitionKey)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    string Esc(string s) => System.Net.WebUtility.HtmlEncode(s);

    // Minimal real field-capture, matching exactly the component types declared as
    // business-user's capability in ReferenceQueues.CapabilitiesProvider() — anything
    // outside this set isn't rendered specially (queue-capability validation is what actually
    // prevents a state from using something unsupported, not this renderer).
    string RenderComponent(PrismComponent component, IReadOnlyDictionary<string, object?> fieldValues) => component switch
    {
        BodyComponent body => $"<p class=\"field-body\">{Esc(body.Content)}</p>",
        PanelComponent panel => $"""<div class="gds-panel"><strong>{Esc(panel.Heading)}</strong></div>""",
        FieldsetComponent fieldset => $"""
            <fieldset>
              {(string.IsNullOrWhiteSpace(fieldset.Legend) ? "" : $"<legend>{Esc(fieldset.Legend)}</legend>")}
              {string.Join("\n", fieldset.Children.Select(c => RenderComponent(c, fieldValues)))}
            </fieldset>
            """,
        TextareaComponent textarea => $"""
            <label class="field-label">{Esc(textarea.Label)}{(textarea.Required ? " *" : "")}
              <textarea name="field:{Esc(textarea.FieldKey)}" {(textarea.Required ? "required" : "")}>{Esc(ExistingOrDefault(textarea, fieldValues))}</textarea>
            </label>
            """,
        DecimalInputComponent number => RenderTextInput(number, "number", fieldValues, step: "any"),
        TextInputComponent text => RenderTextInput(text, "text", fieldValues),
        SummaryListComponent summary => $"""
            <dl class="summary-list">
              {string.Join("\n", summary.Children.OfType<InputComponent>().Select(c => $"""
                <div><dt>{Esc(c.Label)}</dt><dd>{Esc(ExistingOrDefault(c, fieldValues))}</dd></div>
                """))}
            </dl>
            """,
        _ => ""
    };

    string RenderTextInput(InputComponent input, string type, IReadOnlyDictionary<string, object?> fieldValues, string? step = null) => $"""
        <label class="field-label">{Esc(input.Label)}{(input.Required ? " *" : "")}
          <input type="{type}" name="field:{Esc(input.FieldKey)}" value="{Esc(ExistingOrDefault(input, fieldValues))}" {(step is not null ? $"step=\"{step}\"" : "")} {(input.Required ? "required" : "")} />
        </label>
        """;

    string ExistingOrDefault(InputComponent input, IReadOnlyDictionary<string, object?> fieldValues) =>
        fieldValues.TryGetValue(input.FieldKey, out var existing) && existing is not null
            ? existing.ToString() ?? ""
            : input.Default ?? "";

    var instancesById = instances.ToDictionary(i => i.InstanceId, StringComparer.Ordinal);

    var queueRows = businessQueue.Count == 0
        ? """<tr><td colspan="7" style="text-align:center;color:#888;padding:1.5rem">No business-user queue work</td></tr>"""
        : string.Join("\n", businessQueue.Select((item, n) =>
        {
            var shortId = item.InstanceId.Length > 12 ? item.InstanceId[..8] + "…" : item.InstanceId;
            defsByKey.TryGetValue(item.BlueprintKey, out var itemDef);
            var state = itemDef?.Stages.FirstOrDefault(
                s => string.Equals(s.StageKey, item.StageKey, StringComparison.OrdinalIgnoreCase));
            instancesById.TryGetValue(item.InstanceId, out var instanceState);
            var fieldValues = instanceState?.FieldValues ?? new Dictionary<string, object?>();

            var componentsHtml = state is null
                ? ""
                : string.Join("\n", state.Components.Select(c => RenderComponent(c, fieldValues)));

            var buttons = item.AvailableActions.Count == 0
                ? """<span style="color:#888;font-size:.8rem">No actions</span>"""
                : string.Join(" ", item.AvailableActions.Select(action => $"""
                    <button class="btn btn-queue-action" type="submit" name="action" value="{Esc(action.ActionKey)}">{Esc(action.Label)}</button>
                    """));

            var actionsCell = $"""
                <form method="post" action="/admin/service-desk/{Esc(item.InstanceId)}/advance">
                  <input type="hidden" name="stateVersion" value="{item.StateVersion}" />
                  {componentsHtml}
                  <div class="actions">{buttons}</div>
                </form>
                """;

            return $"""
            <tr data-blueprint-key="{Esc(item.BlueprintKey)}" data-queue-name="{Esc(item.QueueName ?? string.Empty)}">
              <td>{n + 1}</td>
              <td style="font-family:monospace;font-size:.8em"><span title="{Esc(item.InstanceId)}">{Esc(shortId)}</span></td>
              <td>
                <strong>{Esc(item.BlueprintDisplayName)}</strong>
                <div style="color:#888;font-size:.73rem">{Esc(item.BlueprintKey)}</div>
              </td>
              <td>
                <span class="badge">{Esc(item.StateDisplayName)}</span>
                <span style="color:#bbb;font-size:.73rem;display:block">{Esc(item.StageKey)}</span>
              </td>
              <td>{Esc(item.QueueName ?? "default")}</td>
              <td>{Esc(item.TenantId)}</td>
              <td>{actionsCell}</td>
            </tr>
            """;
        }));

    var rows = instances.Count == 0
        ? """<tr><td colspan="6" style="text-align:center;color:#888;padding:1.5rem">No service requests</td></tr>"""
        : string.Join("\n", instances.Select((inst, n) =>
        {
            defsByKey.TryGetValue(inst.BlueprintKey, out var def);
            var stateDisplay = def?.Stages
                .FirstOrDefault(s => string.Equals(s.StageKey, inst.CurrentStage, StringComparison.OrdinalIgnoreCase))
                ?.DisplayName ?? inst.CurrentStage;
            var shortId = inst.InstanceId.Length > 12 ? inst.InstanceId[..8] + "…" : inst.InstanceId;
            return $"""
            <tr data-blueprint-key="{Esc(inst.BlueprintKey)}" data-current-state="{Esc(inst.CurrentStage)}">
              <td>{n + 1}</td>
              <td style="font-family:monospace;font-size:.8em"><span title="{Esc(inst.InstanceId)}">{Esc(shortId)}</span></td>
              <td>{Esc(inst.BlueprintKey)}</td>
              <td>
                <span class="badge">{Esc(stateDisplay)}</span>
                <span style="color:#bbb;font-size:.73rem;display:block">{Esc(inst.CurrentStage)}</span>
              </td>
              <td>{Esc(inst.TenantId)}</td>
              <td class="actions">
                <form method="post" action="/admin/service-desk/{Esc(inst.InstanceId)}/reset" style="display:inline">
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
            var authoredServiceBlueprintKey = sourceServiceBlueprintDefinitionKeys.Contains(def.DefinitionKey)
                ? def.DefinitionKey
                : null;
            var editorShortcut = authoredServiceBlueprintKey is not null
                ? $"""<a class="btn btn-edit-service-blueprint" href="/service-blueprint-editor?serviceBlueprint={Esc(authoredServiceBlueprintKey!)}">↗ Edit service blueprint</a>"""
                : """<span class="editor-unavailable" title="This service blueprint currently has no editor definition configured.">No editor definition yet</span>""";
            return $"""
            <tr data-blueprint-key="{Esc(authoredServiceBlueprintKey ?? def.DefinitionKey)}" data-definition-key="{Esc(def.DefinitionKey)}">
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
          <title>Service Desk — MockBusinessApp</title>
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
            .btn-edit-service-blueprint { background:#dbeafe; color:#1d4ed8; }
            .btn-edit-service-blueprint:hover { background:#bfdbfe; }
            .editor-unavailable { display:inline-flex; align-items:center; min-height:30px; padding:.25rem .65rem; border-radius:999px; background:#f3f4f6; color:#4b5563; font-size:.78rem; font-weight:600; }
            .header-links { margin-left:auto; display:flex; gap:.5rem; flex-wrap:wrap; }
            .header-link { display:inline-flex; align-items:center; padding:.45rem .8rem; border-radius:999px; background:rgba(255,255,255,.12); color:#fff; text-decoration:none; font-size:.82rem; font-weight:600; }
            .header-link:hover { background:rgba(255,255,255,.2); }
            .toolbar { margin-bottom:.75rem; display:flex; gap:.5rem; align-items:center; }
            .count { color:#888; font-size:.85rem; }
            .new-service-blueprint-form { display:flex; gap:1rem; align-items:flex-end; background:#fff; border-radius:8px; padding:1rem 1.25rem; box-shadow:0 1px 3px rgba(0,0,0,.08); }
            .new-service-blueprint-form label { display:flex; flex-direction:column; gap:.3rem; font-size:.8rem; color:#555; flex:1; }
            .new-service-blueprint-form input { padding:.5rem .6rem; border:1px solid #d7d9e0; border-radius:5px; font-size:.9rem; }
            .new-service-blueprint-form button { flex-shrink:0; }
            .field-body { margin:.3rem 0; font-size:.85rem; color:#333; }
            .gds-panel { background:#00703c; color:#fff; padding:.6rem .9rem; border-radius:4px; margin:.4rem 0; }
            fieldset { border:1px solid #d7d9e0; border-radius:6px; padding:.6rem .8rem; margin:.4rem 0; }
            legend { font-size:.78rem; font-weight:600; color:#555; padding:0 .3rem; }
            .field-label { display:block; font-size:.78rem; color:#555; margin:.4rem 0; }
            .field-label input, .field-label textarea { display:block; margin-top:.2rem; width:100%; padding:.35rem .5rem; border:1px solid #d7d9e0; border-radius:4px; font-size:.85rem; }
            .summary-list { margin:.4rem 0; }
            .summary-list div { display:flex; justify-content:space-between; gap:1rem; font-size:.82rem; padding:.15rem 0; border-bottom:1px solid #f0f1f4; }
            .summary-list dt { color:#666; }
            .summary-list dd { margin:0; font-weight:600; }
          </style>
        </head>
        <body>
          <header>
            <h1>🔧 Service Desk</h1>
            <span style="opacity:.6;font-size:.85rem">MockBusinessApp — local dev only</span>
            <nav class="header-links" aria-label="Service showcase shortcuts">
              <a class="header-link" href="/service-blueprint-editor">Service Blueprint Editor</a>
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
                  <th>#</th><th>Instance ID</th><th>Service Blueprint</th><th>Queue step</th><th>Queue</th><th>Tenant</th><th></th>
                </tr>
              </thead>
              <tbody>{{queueRows}}</tbody>
            </table>

            <h2>Service Requests</h2>
            <div class="toolbar">
              <span class="count">{{instances.Count}} instance(s)</span>
              <form method="post" action="/admin/service-desk/reset-all" style="margin-left:auto">
                <button class="btn btn-reset-all" onclick="return confirm('Remove ALL instances?')">Reset All</button>
              </form>
            </div>
            <table>
              <thead>
                <tr>
                  <th>#</th><th>Instance ID</th><th>Service Blueprint</th><th>State</th><th>Tenant</th><th></th>
                </tr>
              </thead>
              <tbody>{{rows}}</tbody>
            </table>

            <h2>Service Blueprints</h2>
            <div class="toolbar">
              <span class="count">{{defs.Count}} definition(s)</span>
            </div>
            <table>
              <thead><tr><th>Definition</th><th></th></tr></thead>
              <tbody>{{defRows}}</tbody>
            </table>

            <h2>Add a new service</h2>
            <form method="post" action="/admin/service-desk/create" class="new-service-blueprint-form">
              <label>
                Definition key
                <input type="text" name="definitionKey" placeholder="garden-waste-permit" pattern="[a-zA-Z0-9_\-]+" required />
              </label>
              <label>
                Display name
                <input type="text" name="displayName" placeholder="Garden Waste Permit" required />
              </label>
              <button class="btn btn-edit-service-blueprint" type="submit">+ Create service blueprint</button>
            </form>
          </main>
        </body>
        </html>
        """;

    return Results.Content(html, "text/html");
});

app.MapPost("/admin/service-desk/{instanceId}/advance", async (string instanceId, HttpContext context, BusinessAppProcessManager engine) =>
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

    // Recover the state's declared component types so a "decimal" field posts as a real
    // number rather than a raw string — everything else (text/textarea) passes through as-is.
    var definition = engine.GetDefinition(instance.BlueprintKey);
    var state = definition?.Stages.FirstOrDefault(
        s => string.Equals(s.StageKey, instance.CurrentStage, StringComparison.OrdinalIgnoreCase));
    var decimalFieldKeys = state?.Components.GetAllInputs()
        .Where(c => c is DecimalInputComponent)
        .Select(c => c.FieldKey)
        .ToHashSet(StringComparer.Ordinal) ?? [];

    var fieldValues = form
        .Where(kv => kv.Key.StartsWith("field:", StringComparison.Ordinal))
        .ToDictionary(
            kv => kv.Key["field:".Length..],
            object? (kv) => decimalFieldKeys.Contains(kv.Key["field:".Length..]) && decimal.TryParse(kv.Value, out var d)
                ? d
                : kv.Value.ToString());

    engine.Advance(
        instanceId,
        instance.TenantId,
        instance.UserId,
        ReferenceQueues.BusinessUserProfile(),
        action,
        stateVersion,
        fieldValues: fieldValues.Count > 0 ? fieldValues : null);

    return Results.Redirect("/admin/service-desk");
});

app.MapPost("/admin/service-desk/{instanceId}/reset", (string instanceId, BusinessAppProcessManager engine) =>
{
    engine.Reset(instanceId);
    return Results.Redirect("/admin/service-desk");
});

app.MapPost("/admin/service-desk/reset-all", (BusinessAppProcessManager engine) =>
{
    engine.ResetAll();
    return Results.Redirect("/admin/service-desk");
});

// Scaffolds a brand-new, state-less service blueprint shell and hands off straight to the editor — the
// on-screen counterpart to what a script previously had to do off-camera via a raw PUT. Genuinely
// generic, not demo-specific: the graph's own "add stage" affordance sets `initialState` to the
// first stage's key the moment one gets created (see prism-service-blueprint-graph.ts), so an empty
// `stages`/`initialStage` shell is a real, supported starting point for any new service blueprint, not a
// special case this endpoint invents. Memory-only, same as every other authoring write in this
// reference app — nothing here touches disk.
app.MapPost("/admin/service-desk/create", async (HttpRequest request, IServiceBlueprintSourceStore store, CancellationToken ct) =>
{
    var form = await request.ReadFormAsync(ct);
    var definitionKey = form["definitionKey"].ToString().Trim();
    var displayName = form["displayName"].ToString().Trim();

    if (!System.Text.RegularExpressions.Regex.IsMatch(definitionKey, @"^[a-zA-Z0-9_\-]+$") || displayName.Length == 0)
    {
        return Results.Problem(
            detail: "Definition key must be letters/numbers/hyphens/underscores only, and display name can't be empty.",
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid new-service-blueprint request");
    }

    var existing = await store.LoadAsync(definitionKey, ct);
    if (existing is not null)
    {
        // Already exists — nothing to scaffold, just take the author to it.
        return Results.Redirect($"/service-blueprint-editor?serviceBlueprint={Uri.EscapeDataString(definitionKey)}");
    }

    var shell = new ServiceBlueprint
    {
        DefinitionKey = definitionKey,
        DisplayName = displayName,
        Version = 0,
        InitialStage = "",
        RequestPolicy = "single",
        Stages = [],
        Queues = [new QueueDefinition { Key = "web-user", DisplayName = "Member", Actor = "member" }]
    };

    await store.SaveAsync(shell, expectedVersion: 0, ct);
    return Results.Redirect($"/service-blueprint-editor?serviceBlueprint={Uri.EscapeDataString(definitionKey)}");
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

public record ServiceRequestCurrentApiRequest(
    string? InstanceId,
    string? Action);

public record ServiceRequestAdvanceApiRequest(
    string InstanceId,
    string Action,
    int StateVersion,
    Dictionary<string, object?>? FieldValues);

// Passthrough sanitizer: seed content is developer-authored, not user-supplied.
// No XSS risk — passthrough is intentional and appropriate for this mock app.
file sealed class PassthroughSanitizer : IServiceContentSanitizer
{
    public string Sanitize(string? html) => html ?? string.Empty;
}
