using System.Runtime.InteropServices;

var builder = DistributedApplication.CreateBuilder(args);

var needsKeycloakSveWorkaround =
    OperatingSystem.IsMacOS() &&
    RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

// Add Keycloak as a container resource
var keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "26.0.0")
    .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http")
    .WithHttpsEndpoint(port: 8443, targetPort: 8080, name: "https")
    .WithEnvironment("KEYCLOAK_ADMIN", "admin")
    .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", "admin")
    .WithEnvironment("KC_HEALTH_ENABLED", "true")
    .WithEnvironment("KC_METRICS_ENABLED", "true")
    .WithBindMount("../../keycloak", "/opt/keycloak/data/import")
    .WithArgs("start-dev", "--import-realm", "--proxy-headers", "xforwarded", "--server-async-bootstrap=false");

if (needsKeycloakSveWorkaround)
{
    keycloak = keycloak.WithEnvironment("JAVA_OPTS_APPEND", "-XX:UseSVE=0");
}

// Add TestSite with environment variable pointing to Keycloak.
// The Umbraco project uses a custom launch profile name, so select it explicitly
// so Aspire can discover the applicationUrl endpoints and advertise them.
builder.AddProject("testsite", "../UmbracoPrism.TestSite/UmbracoPrism.TestSite.csproj", launchProfileName: "Umbraco.Web.UI")
    .WithEnvironment("KEYCLOAK_URL", keycloak.GetEndpoint("https"))
    .WaitFor(keycloak);

// Add MockBusinessApp — runs alongside TestSite for the full dev stack
builder.AddProject("businessapp", "../UmbracoPrism.MockBusinessApp/UmbracoPrism.MockBusinessApp.csproj");

builder.Build().Run();
