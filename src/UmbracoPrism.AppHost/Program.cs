var builder = DistributedApplication.CreateBuilder(args);

// Add Keycloak as a container resource
var keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "26.0.0")
    .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http")
    .WithEnvironment("KEYCLOAK_ADMIN", "admin")
    .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", "admin")
    .WithEnvironment("KC_HEALTH_ENABLED", "true")
    .WithEnvironment("KC_METRICS_ENABLED", "true")
    .WithBindMount("../../keycloak", "/opt/keycloak/data/import")
    .WithArgs("start-dev", "--import-realm");

// Add TestSite with environment variable pointing to Keycloak
builder.AddProject("testsite", "../UmbracoPrism.TestSite/UmbracoPrism.TestSite.csproj")
    .WithEnvironment("KEYCLOAK_URL", () => $"http://{keycloak.Resource.Name}:8080")
    .WaitFor(keycloak);

// Add MockBusinessApp — runs alongside TestSite for the full dev stack
builder.AddProject("businessapp", "../UmbracoPrism.MockBusinessApp/UmbracoPrism.MockBusinessApp.csproj");

builder.Build().Run();
