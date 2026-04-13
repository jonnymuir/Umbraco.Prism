using System.Runtime.InteropServices;

var builder = DistributedApplication.CreateBuilder(args);

var needsKeycloakSveWorkaround =
    OperatingSystem.IsMacOS() &&
    RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

// Add Keycloak as a container resource on HTTP 8080.
var keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "26.0.0")
    .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http")
    .WithEnvironment("KEYCLOAK_ADMIN", "admin")
    .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", "admin")
    .WithEnvironment("KC_HEALTH_ENABLED", "true")
    .WithEnvironment("KC_METRICS_ENABLED", "true")
    .WithBindMount("../../keycloak", "/opt/keycloak/data/import")
    .WithArgs("start-dev", "--import-realm", "--proxy-headers", "xforwarded");

if (needsKeycloakSveWorkaround)
{
    keycloak = keycloak.WithEnvironment("JAVA_OPTS_APPEND", "-XX:UseSVE=0");
}

// Add HTTPS proxy for Keycloak that terminates TLS at localhost:8443.
// The proxy creates a self-signed certificate on startup and forwards requests
// to Keycloak's HTTP endpoint with X-Forwarded headers so Keycloak knows the
// external origin is HTTPS. This enables Safari/WebKit-safe authentication flows.
var keycloakProxy = builder.AddProject<Projects.UmbracoPrism_KeycloakProxy>("keycloak-proxy")
    .WaitFor(keycloak);

// Add TestSite with environment variable pointing to Keycloak HTTPS proxy.
// The Umbraco project uses a custom launch profile name, so select it explicitly
// so Aspire can discover the applicationUrl endpoints and advertise them.
builder.AddProject("testsite", "../UmbracoPrism.TestSite/UmbracoPrism.TestSite.csproj", launchProfileName: "Umbraco.Web.UI")
    .WithEnvironment("KEYCLOAK_URL", "https://localhost:8443")
    .WaitFor(keycloakProxy);

// Add MockBusinessApp — runs alongside TestSite for the full dev stack
builder.AddProject("businessapp", "../UmbracoPrism.MockBusinessApp/UmbracoPrism.MockBusinessApp.csproj");

builder.Build().Run();
