using UmbracoPrism.Core.Models;

namespace UmbracoPrism.MockBusinessApp.Services;

/// <summary>Development-only troubleshooting. Nothing here is mapped in any other environment.</summary>
public static class Diagnostics
{
    /// <summary>
    /// <c>GET /debug/auth</c>: which tenants and backchannel this instance is configured with, and whether
    /// the backchannel answers. Deliberately anonymous, because its purpose is diagnosing why bearer
    /// validation fails, and it is only ever mapped when <paramref name="app"/> is in Development, so
    /// the route does not exist in a deployed environment.
    /// </summary>
    public static void MapDevelopmentDiagnostics(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        var environment = app.Environment;
        app.MapGet("/debug/auth", (IConfiguration config) =>
        {
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
                        backchannelProbe = $"{(int)resp.StatusCode} {resp.StatusCode}: {metaUrl}";
                    }
                }
                catch (Exception ex)
                {
                    backchannelProbe = $"ERROR: {ex.Message}";
                }
            }

            return Results.Ok(new
            {
                environment             = environment.EnvironmentName,
                aspNetCoreEnvironment   = aspNetCoreEnv ?? "(not set)",
                codespaceName           = codespaceName ?? "(not set)",
                backchannelUrl          = backchannelUrl ?? "(not set)",
                backchannelJwksEnabled,
                backchannelProbe,
                tenants,
            });
        }).AllowAnonymous();
    }
}
