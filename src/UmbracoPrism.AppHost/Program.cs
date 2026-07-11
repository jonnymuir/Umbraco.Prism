using System.Runtime.InteropServices;

var builder = DistributedApplication.CreateBuilder(args);

// CODESPACE_NAME is a built-in GitHub Codespaces env var available to all processes
// (no ~/.bashrc dependency). Use it to derive the public Keycloak and TestSite URLs.
var codespaceName = Environment.GetEnvironmentVariable("CODESPACE_NAME");
var codespacePortDomain = Environment.GetEnvironmentVariable("GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN") ?? "app.github.dev";

// Keycloak proxy public URL, TestSite public URL, and BusinessApp public URL.
// In Codespaces: discovered via `gh codespace ports` (authoritative for both legacy and
// new regional URL schemes where the opaque token ≠ CODESPACE_NAME). Falls back to the
// legacy `{CODESPACE_NAME}-{port}.{domain}` pattern when gh is unavailable.
// Outside Codespaces: KEYCLOAK_URL env var or localhost.
string keycloakProxyUrl;
string? testSitePublicUrl;
string businessAppUrl;

if (codespaceName != null)
{
    (keycloakProxyUrl, testSitePublicUrl, businessAppUrl) = TryDiscoverCodespaceUrls(codespaceName, codespacePortDomain);
}
else
{
    keycloakProxyUrl = Environment.GetEnvironmentVariable("KEYCLOAK_URL") ?? "https://localhost:8443";
    testSitePublicUrl = null;
    businessAppUrl = "https://localhost:7245";
}

var keycloakProxyUri = new Uri(keycloakProxyUrl);
var keycloakHostname = keycloakProxyUri.Host;
// -1 means "use the scheme's default port" (443 for HTTPS). For local dev on 8443 we pass the explicit port.
var keycloakHostnamePort = keycloakProxyUri.IsDefaultPort ? "-1" : keycloakProxyUri.Port.ToString();
// X-Forwarded-Host value for the YARP proxy: host:port for non-default ports (local dev),
// or just host for standard HTTPS (Codespaces — port 443 is implied by the scheme).
var keycloakExternalHost = keycloakProxyUri.IsDefaultPort
    ? keycloakHostname
    : $"{keycloakHostname}:{keycloakProxyUri.Port}";
const string TestSiteRuntimeRootEnvironmentVariable = "PRISM_TESTSITE_RUNTIME_ROOT";
const string ResetTestSiteRuntimeEnvironmentVariable = "PRISM_TESTSITE_RESET_RUNTIME";

var defaultTestSiteRuntimeRoot = Path.GetFullPath(
    Path.Combine(builder.Environment.ContentRootPath, "..", "..", "artifacts", "aspire", "testsite-runtime"));
var defaultKeycloakDataRoot = Path.GetFullPath(
    Path.Combine(builder.Environment.ContentRootPath, "..", "..", "artifacts", "aspire", "keycloak-data"));
var testSiteRuntimeRoot =
    Environment.GetEnvironmentVariable(TestSiteRuntimeRootEnvironmentVariable) ?? defaultTestSiteRuntimeRoot;
var keycloakDataRoot =
    Environment.GetEnvironmentVariable("PRISM_KEYCLOAK_DATA_ROOT") ?? defaultKeycloakDataRoot;
var resetTestSiteRuntime =
    Environment.GetEnvironmentVariable(ResetTestSiteRuntimeEnvironmentVariable) ?? bool.FalseString;

var needsKeycloakSveWorkaround =
    OperatingSystem.IsMacOS() &&
    RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

// Add Keycloak as a container resource with an ephemeral HTTP port.
// Data directory is bind-mounted to persist sessions/tokens across restarts.
// HTTP health check probes the realm's OIDC discovery endpoint to ensure both
// Keycloak startup and realm import have completed before dependent services start.
// NOTE: port is null to let Aspire assign an ephemeral port in Codespaces and local dev.
// The actual runtime port is retrieved via keycloak.GetEndpoint("http") and passed to
// services that need backchannel access (TestSite, MockBusinessApp, KeycloakProxy).
var keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "26.0.0")
    .WithHttpEndpoint(port: null, targetPort: 8080, name: "http")
    .WithHttpHealthCheck("/realms/prism-dev/.well-known/openid-configuration")
    .WithEnvironment("KEYCLOAK_ADMIN", "admin")
    .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", "admin")
    .WithEnvironment("KC_HEALTH_ENABLED", "true")
    .WithEnvironment("KC_METRICS_ENABLED", "true")
    .WithEnvironment("KC_HOSTNAME", keycloakHostname)
    .WithEnvironment("KC_HOSTNAME_PORT", keycloakHostnamePort)
    .WithBindMount("../../keycloak", "/opt/keycloak/data/import")
    .WithBindMount(keycloakDataRoot, "/opt/keycloak/data/h2")
    .WithArgs("start-dev", "--import-realm", "--proxy-headers", "xforwarded");

if (needsKeycloakSveWorkaround)
{
    keycloak = keycloak.WithEnvironment("JAVA_OPTS_APPEND", "-XX:UseSVE=0");
}

// Add HTTPS proxy for Keycloak that terminates TLS at localhost:8443.
// Uses the .NET dev certificate (already trusted via `dotnet dev-certs https --trust`)
// and forwards requests to Keycloak's HTTP endpoint with X-Forwarded headers so
// Keycloak knows the external origin is HTTPS. Enables Safari/WebKit-safe auth flows.
var keycloakProxy = builder.AddProject("keycloak-proxy", "../UmbracoPrism.KeycloakProxy/UmbracoPrism.KeycloakProxy.csproj", launchProfileName: "https")
    .WithEnvironment("ReverseProxy__Clusters__keycloak-cluster__Destinations__keycloak__Address", keycloak.GetEndpoint("http"))
    // Override the hardcoded X-Forwarded-Host in appsettings.json with the correct public hostname.
    // Keycloak 26 with --proxy-headers xforwarded uses X-Forwarded-Host for form action URLs;
    // it must match what the browser uses so login POSTs reach the right address.
    .WithEnvironment("ReverseProxy__Routes__keycloak__Transforms__1__Set", keycloakExternalHost)
    .WaitFor(keycloak);

// Add TestSite with environment variable pointing to Keycloak HTTPS proxy.
// The Umbraco project uses a custom launch profile name, so select it explicitly
// so Aspire can discover the applicationUrl endpoints and advertise them.
var businessApp = builder.AddProject("businessapp", "../UmbracoPrism.MockBusinessApp/UmbracoPrism.MockBusinessApp.csproj", launchProfileName: "https")
    .WithEnvironment("PrismBusinessApp__Tenants__2__OidcAuthority", $"{keycloakProxyUrl}/realms/prism-dev")
    .WithUrls(ctx =>
    {
        var baseUrl = ctx.Urls
            .Where(u => u.Url?.StartsWith("https://", StringComparison.OrdinalIgnoreCase) == true)
            .Select(u => new Uri(u.Url!))
            .Select(uri => $"{uri.Scheme}://{uri.Authority}")
            .FirstOrDefault();

        if (baseUrl != null)
        {
            ctx.Urls.Add(new ResourceUrlAnnotation
            {
                Url = $"{baseUrl}/admin/workflow",
                DisplayText = "Workflow Admin",
                DisplayOrder = 1,
            });

            ctx.Urls.Add(new ResourceUrlAnnotation
            {
                Url = $"{baseUrl}/workflow-editor",
                DisplayText = "Workflow Editor",
                DisplayOrder = 2,
            });

            ctx.Urls.Add(new ResourceUrlAnnotation
            {
                Url = $"{baseUrl}/prism/workflow-authoring/mcp",
                DisplayText = "Workflow Authoring MCP",
                DisplayOrder = 3,
            });
        }
    });

var prismClient = builder.AddNpmApp("prism-client", "../UmbracoPrism.Client", "build");

var testsite = builder.AddProject("testsite", "../UmbracoPrism.TestSite/UmbracoPrism.TestSite.csproj", launchProfileName: "Umbraco.Web.UI")
    .WithEnvironment("KEYCLOAK_URL", keycloakProxyUrl)
    .WithEnvironment("PrismBusinessApp__WorkflowApiBaseUrl", businessAppUrl)
    .WithEnvironment(TestSiteRuntimeRootEnvironmentVariable, testSiteRuntimeRoot)
    .WithEnvironment(ResetTestSiteRuntimeEnvironmentVariable, resetTestSiteRuntime)
    .WaitFor(keycloakProxy)
    .WaitFor(businessApp)
    .WaitFor(prismClient);

// In Codespaces, tell the TestSite its public URL so OIDC generates the correct redirect_uri.
if (testSitePublicUrl != null)
    testsite.WithEnvironment("TESTSITE_PUBLIC_URL", testSitePublicUrl);

// In Codespaces, the GitHub forwarded-port proxy blocks unauthenticated server-side backchannel
// calls to the external Keycloak URL. Point token exchange at Keycloak's internal HTTP endpoint
// directly so it bypasses the proxy (and avoids any localhost dev-cert trust issues on Ubuntu).
if (codespaceName != null)
    testsite.WithEnvironment("KEYCLOAK_BACKCHANNEL_URL", keycloak.GetEndpoint("http"));

// In Codespaces, server-side calls from TestSite to the public app.github.dev BusinessApp URL can
// hit the GitHub forwarded-port proxy and receive an HTML tunnel/auth page instead of API JSON
// because the forwarded port is browser-facing and often private. Use the BusinessApp's internal
// HTTP endpoint for server-to-server traffic; keep PrismBusinessApp__WorkflowApiBaseUrl as the
// browser/public URL only.
// NOTE: Aspire may assign ephemeral ports, so use GetEndpoint("http") for dynamic discovery.
if (codespaceName != null)
    testsite.WithEnvironment("BUSINESSAPP_BACKCHANNEL_URL", businessApp.GetEndpoint("http"));

// BusinessApp also fetches Keycloak signing keys server-side for JWT Bearer validation.
// Same backchannel fix: without this the external Keycloak URL is blocked by the
// GitHub Codespaces proxy and every Bearer token is rejected with 401.
if (codespaceName != null)
    businessApp.WithEnvironment("KEYCLOAK_BACKCHANNEL_URL", keycloak.GetEndpoint("http"));

builder.Build().Run();

// ── Codespaces URL discovery helpers ─────────────────────────────────────────
// Queries `gh codespace ports` for the authoritative public browseUrl of each
// forwarded port. Works with both the legacy scheme ({CODESPACE_NAME}-{port}.app.github.dev)
// and the new regional scheme ({opaque-token}-{port}.{region}.app.github.dev) where the
// token is not derivable from CODESPACE_NAME. Falls back to the legacy string pattern
// when gh is unavailable so non-Codespaces local dev environments still work.

static (string keycloakUrl, string? testSiteUrl, string businessAppUrl) TryDiscoverCodespaceUrls(string codespaceName, string domain)
{
    try
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "gh",
            Arguments = $"codespace ports --codespace {codespaceName} --json sourcePort,browseUrl",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(psi);
        if (process is null)
            return FallbackCodespaceUrls(codespaceName, domain, "gh process failed to start");

        var json = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(10_000);

        if (process.ExitCode != 0)
        {
            var reason = string.IsNullOrWhiteSpace(stderr)
                ? $"gh exited with code {process.ExitCode}"
                : $"gh exited with code {process.ExitCode}: {stderr.Trim()}";
            return FallbackCodespaceUrls(codespaceName, domain, reason);
        }

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        string? keycloakUrl = null, testSiteUrl = null, businessAppUrl = null;

        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            if (!entry.TryGetProperty("sourcePort", out var portEl) ||
                !entry.TryGetProperty("browseUrl", out var urlEl))
                continue;

            var port = portEl.GetInt32();
            var url = urlEl.GetString()?.TrimEnd('/');
            if (url is null) continue;

            if (port == 8443)  keycloakUrl = url;
            if (port == 44345) testSiteUrl = url;
            if (port == 7245)  businessAppUrl = url;
        }

        if (keycloakUrl is null)
            return FallbackCodespaceUrls(codespaceName, domain, "port 8443 not found in gh codespace ports output");

        // Derive missing port URLs from discovered URLs using the same host pattern
        // (e.g., v7ldkc4c-8443.uks1.app.github.dev → v7ldkc4c-7245.uks1.app.github.dev)
        businessAppUrl ??= DeriveCodespaceUrl(keycloakUrl, 7245);

        Console.WriteLine(
            $"[PRISM] Discovered Codespaces URLs — Keycloak: {keycloakUrl}  TestSite: {testSiteUrl ?? "(port 44345 not yet forwarded)"}  BusinessApp: {businessAppUrl}");
        return (keycloakUrl, testSiteUrl, businessAppUrl);
    }
    catch (Exception ex)
    {
        return FallbackCodespaceUrls(codespaceName, domain, ex.Message);
    }
}

static (string keycloakUrl, string? testSiteUrl, string businessAppUrl) FallbackCodespaceUrls(string codespaceName, string domain, string reason)
{
    Console.WriteLine(
        $"[PRISM] WARNING: Could not discover Codespaces URLs via gh CLI ({reason}). " +
        $"Falling back to legacy URL pattern — will not work if this Codespace uses the regional URL scheme.");
    return (
        $"https://{codespaceName}-8443.{domain}",
        $"https://{codespaceName}-44345.{domain}",
        $"https://{codespaceName}-7245.{domain}"
    );
}

// Derives a Codespaces public URL for a different port by replacing the port number
// in a known URL's hostname. Works with both legacy ({name}-{port}.app.github.dev)
// and regional ({token}-{port}.{region}.app.github.dev) URL schemes.
static string DeriveCodespaceUrl(string knownUrl, int targetPort)
{
    var uri = new Uri(knownUrl);
    var hostname = uri.Host;
    
    // Find the last dash before the first dot (port separator in Codespaces URLs)
    var firstDot = hostname.IndexOf('.');
    if (firstDot == -1)
        return $"https://{hostname}"; // Unexpected format, return as-is
    
    var lastDash = hostname.LastIndexOf('-', firstDot);
    if (lastDash == -1)
        return $"https://{hostname}"; // Unexpected format, return as-is
    
    // Extract the port substring and validate it's actually a number
    var portSubstring = hostname.Substring(lastDash + 1, firstDot - lastDash - 1);
    if (!int.TryParse(portSubstring, out _))
        return $"https://{hostname}"; // Not a valid port, return as-is
    
    // Replace port: {prefix}-{oldPort}.{suffix} → {prefix}-{newPort}.{suffix}
    var prefix = hostname.Substring(0, lastDash);
    var suffix = hostname.Substring(firstDot);
    return $"https://{prefix}-{targetPort}{suffix}";
}
