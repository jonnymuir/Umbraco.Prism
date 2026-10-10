using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using UmbracoPrism.Core.Extensions;
using UmbracoPrism.MockBusinessApp.Services;
using UmbracoPrism.MockBusinessApp.Services.Members;
using UmbracoPrism.MockBusinessApp.Services.Profile;
using UmbracoPrism.MockBusinessApp.Services.SupportSystem;

var builder = WebApplication.CreateBuilder(args);

// Nothing this app accepts is larger than a small CSV, so cap every request body well below Kestrel's
// 30 MB default. Routes enforce their own, tighter limits (see ContributionsEndpoints).
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 2 * 1024 * 1024);

// Local secrets override — gitignored. Supply real Entra tenant/client IDs and member
// emails here. See src/UmbracoPrism.MockBusinessApp/README.md for setup instructions.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddPrismAuthentication(builder.Configuration);

// Deny by default: a route with no policy of its own still demands an authenticated caller, so a
// forgotten RequireAuthorization() fails closed. Anonymous access has to be written down with an
// explicit AllowAnonymous() and a reason, as Diagnostics.MapDevelopmentDiagnostics does.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// Per caller (tenant + email from the validated token), or per remote address when there is no valid
// token, so a flood of forged tokens cannot use up a real member's allowance. Behind a reverse proxy
// the remote address is the proxy's unless forwarded headers are configured for it.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var caller = context.User.Identity?.IsAuthenticated == true
            ? CallerIdentity.From(context.User, context.RequestServices.GetRequiredService<IConfiguration>())
            : null;

        return caller is not null
            ? RateLimitPartition.GetFixedWindowLimiter($"caller:{caller.OwnerKey}",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1) })
            : RateLimitPartition.GetFixedWindowLimiter($"anonymous:{context.Connection.RemoteIpAddress}",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) });
    });
});

// A real, separate business application that Wayfinder.Umbraco-hosted journeys call as the signed-in
// member. Front-stage (citizen, Umbraco CMS) and back-stage (caseworker) hosting both live in
// Wayfinder.Umbraco, in-process; this app has no engine of its own.
builder.Services.AddSingleton<ContributionsStore>();
builder.Services.AddSingleton<ProfileStore>();
builder.Services.AddSingleton<MemberRegistry>();
builder.Services.AddSingleton<MemberDirectory>();
builder.Services.AddSingleton<MemberRegistrar>();

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

// A bearer token sent over plain HTTP is already compromised, so outside Development the app refuses
// the request instead of redirecting it. Terminate TLS in front of the app (and configure forwarded
// headers for that proxy) rather than serving HTTP.
if (!app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        if (!context.Request.IsHttps)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        await next();
    });
}

// Responses are per-caller JSON: never cache them, and never let a browser sniff a type.
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.CacheControl = "no-store";
    await next();
});

// Authentication runs before the rate limiter so the limiter can partition by the validated caller,
// and before authorization, which needs the principal.
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

// Every route below acts for the caller identified by the validated bearer token: tenant and member
// come from its claims, never the request. See also the fallback policy above.
app.MapContributions();
app.MapProfile();
app.MapMembers();

app.MapGet("/api/backoffice/me", (IConfiguration config, ClaimsPrincipal user, HttpContext context, MemberDirectory directory, ILogger<Program> logger) =>
{
    logger.LogInformation(
        "BusinessApp handler entry: {Method} {Path} trace={TraceIdentifier} authHeaderPresent={AuthHeaderPresent} callerTraceId={CallerTraceId} userAuthenticated={UserAuthenticated}",
        context.Request.Method,
        context.Request.Path.Value ?? "/",
        context.TraceIdentifier,
        context.Request.Headers.ContainsKey("Authorization"),
        GetCallerTraceId(context.Request),
        user.Identity?.IsAuthenticated ?? false);

    var caller = CallerIdentity.From(user, config);
    if (caller is null) return Results.Problem("Tenant not recognised, or the token carries no email.");

    var member = directory.Find(caller);

    return Results.Ok(new
    {
        Tenant = caller.Tenant.DisplayName,
        TenantCode = caller.Tenant.Code,
        UserEmail = caller.Email,
        IsRegistered = member != null,
        BackOfficeId = member?.BackOfficeId ?? "N/A",
        AssignedRole = member?.Role ?? "Guest"
    });
}).RequireAuthorization();

app.MapDevelopmentDiagnostics();

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

// The seam the host-level tests boot the real app through (WebApplicationFactory<MockBusinessAppEntryPoint>).
// A uniquely named type, because other apps in this repo also have a Program.
public sealed class MockBusinessAppEntryPoint;
