using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using UmbracoPrism.Core.Extensions;
using UmbracoPrism.MockBusinessApp.Services.SupportSystem;

var builder = WebApplication.CreateBuilder(args);

// Local secrets override — gitignored. Supply real Entra tenant/client IDs and member
// emails here. See src/UmbracoPrism.MockBusinessApp/README.md for setup instructions.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddPrismAuthentication(builder.Configuration);

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

// MockBusinessApp's sole remaining purpose: a real, separate downstream support system — see
// docs/guides/support-systems.md in the core Wayfinder repo. Front-stage (citizen, Umbraco CMS)
// and back-stage (caseworker) hosting both now live entirely in Wayfinder.Umbraco, in-process —
// this app has no engine of its own any more.
builder.Services.AddSingleton<SupportSystemStore>();
builder.Services.AddSingleton<ContributionsStore>();

var app = builder.Build();

// SECURITY: KEYCLOAK_BACKCHANNEL_URL must never be set in production — it bypasses
// TLS certificate validation for OIDC metadata fetches, which is only acceptable
// in controlled development environments. Fail loudly if misconfigured.
if (!app.Environment.IsDevelopment() && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KEYCLOAK_BACKCHANNEL_URL")))
{
    throw new InvalidOperationException("KEYCLOAK_BACKCHANNEL_URL must not be set in non-Development environments.");
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

// The support system itself — see docs/guides/support-systems.md in the core Wayfinder repo.
// Intentionally no auth here: a real downstream support system's own auth model is its business,
// not something Wayfinder or Prism prescribes — this mirrors SafetyNetUnderwriting's own
// reference-app posture in the core Wayfinder repo.
app.MapSupportSystem();
app.MapContributions();

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
