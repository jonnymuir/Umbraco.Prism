using System.Runtime.InteropServices;

var builder = DistributedApplication.CreateBuilder(args);

// CODESPACE_NAME is a built-in GitHub Codespaces env var available to all processes
// (no ~/.bashrc dependency). Use it to derive the public Keycloak and TestSite URLs.
var codespaceName = Environment.GetEnvironmentVariable("CODESPACE_NAME");
var codespacePortDomain = Environment.GetEnvironmentVariable("GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN") ?? "app.github.dev";

// Keycloak proxy public URL: Codespace-forwarded address when in Codespaces,
// otherwise KEYCLOAK_URL env var (for custom deployments), or localhost for local dev.
var keycloakProxyUrl = codespaceName != null
    ? $"https://{codespaceName}-8443.{codespacePortDomain}"
    : (Environment.GetEnvironmentVariable("KEYCLOAK_URL") ?? "https://localhost:8443");

var keycloakProxyUri = new Uri(keycloakProxyUrl);
var keycloakHostname = keycloakProxyUri.Host;
// -1 means "use the scheme's default port" (443 for HTTPS). For local dev on 8443 we pass the explicit port.
var keycloakHostnamePort = keycloakProxyUri.IsDefaultPort ? "-1" : keycloakProxyUri.Port.ToString();
// X-Forwarded-Host value for the YARP proxy: host:port for non-default ports (local dev),
// or just host for standard HTTPS (Codespaces — port 443 is implied by the scheme).
var keycloakExternalHost = keycloakProxyUri.IsDefaultPort
    ? keycloakHostname
    : $"{keycloakHostname}:{keycloakProxyUri.Port}";

// In Codespaces, the TestSite's public URL is needed so OIDC generates the correct
// redirect_uri — GitHub Codespaces does not forward the public hostname in the Host header.
var testSitePublicUrl = codespaceName != null
    ? $"https://{codespaceName}-44345.{codespacePortDomain}"
    : null;
const string BusinessAppUrl = "https://localhost:7245";
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

// Add Keycloak as a container resource on HTTP 8080.
// Data directory is bind-mounted to persist sessions/tokens across restarts.
// HTTP health check probes the realm's OIDC discovery endpoint to ensure both
// Keycloak startup and realm import have completed before dependent services start.
var keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "26.0.0")
    .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http")
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
    .WithEnvironment("PrismBusinessApp__Tenants__2__OidcAuthority", $"{keycloakProxyUrl}/realms/prism-dev");

var testsite = builder.AddProject("testsite", "../UmbracoPrism.TestSite/UmbracoPrism.TestSite.csproj", launchProfileName: "Umbraco.Web.UI")
    .WithEnvironment("KEYCLOAK_URL", keycloakProxyUrl)
    .WithEnvironment("PrismBusinessApp__WorkflowApiBaseUrl", BusinessAppUrl)
    .WithEnvironment(TestSiteRuntimeRootEnvironmentVariable, testSiteRuntimeRoot)
    .WithEnvironment(ResetTestSiteRuntimeEnvironmentVariable, resetTestSiteRuntime)
    .WaitFor(keycloakProxy)
    .WaitFor(businessApp);

// In Codespaces, tell the TestSite its public URL so OIDC generates the correct redirect_uri.
if (testSitePublicUrl != null)
    testsite.WithEnvironment("TESTSITE_PUBLIC_URL", testSitePublicUrl);

// In Codespaces, the GitHub forwarded-port proxy blocks unauthenticated server-side backchannel
// calls to the external Keycloak URL. Point token exchange at Keycloak's internal HTTP endpoint
// directly so it bypasses the proxy (and avoids any localhost dev-cert trust issues on Ubuntu).
if (codespaceName != null)
    testsite.WithEnvironment("KEYCLOAK_BACKCHANNEL_URL", keycloak.GetEndpoint("http"));

builder.Build().Run();
