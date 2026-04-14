using System.Runtime.InteropServices;

var builder = DistributedApplication.CreateBuilder(args);
const string KeycloakProxyUrl = "https://localhost:8443";
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
    .WaitFor(keycloak);

// Add TestSite with environment variable pointing to Keycloak HTTPS proxy.
// The Umbraco project uses a custom launch profile name, so select it explicitly
// so Aspire can discover the applicationUrl endpoints and advertise them.
var businessApp = builder.AddProject("businessapp", "../UmbracoPrism.MockBusinessApp/UmbracoPrism.MockBusinessApp.csproj", launchProfileName: "https")
    .WithEnvironment("PrismBusinessApp__Tenants__2__OidcAuthority", $"{KeycloakProxyUrl}/realms/prism-dev");

builder.AddProject("testsite", "../UmbracoPrism.TestSite/UmbracoPrism.TestSite.csproj", launchProfileName: "Umbraco.Web.UI")
    .WithEnvironment("KEYCLOAK_URL", KeycloakProxyUrl)
    .WithEnvironment("PrismBusinessApp__WorkflowApiBaseUrl", BusinessAppUrl)
    .WithEnvironment(TestSiteRuntimeRootEnvironmentVariable, testSiteRuntimeRoot)
    .WithEnvironment(ResetTestSiteRuntimeEnvironmentVariable, resetTestSiteRuntime)
    .WaitFor(keycloakProxy)
    .WaitFor(businessApp);

builder.Build().Run();
