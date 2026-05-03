---
name: "aspire-dynamic-endpoint-backchannels"
description: "Use Aspire's GetEndpoint() for backchannel URLs to handle dynamic port assignment in Codespaces"
domain: "orchestration"
confidence: "high"
source: "earned"
tools:
  - name: "bash"
    description: "Test backchannel connectivity and verify port assignments."
    when: "Use when diagnosing backchannel timeouts or validating dynamic endpoint discovery."
---

## Context

Use this when server-to-server backchannel calls timeout in Codespaces or when hardcoded localhost ports don't match the actual runtime endpoints.

Aspire may assign ephemeral ports for HTTP/HTTPS endpoints, especially in containerized environments like GitHub Codespaces. Hardcoding `http://localhost:5163` in AppHost configuration fails when Aspire binds to a different port.

## Pattern

For server-to-server backchannel URLs, use Aspire's `GetEndpoint()` for dynamic port discovery:

```csharp
// ✅ GOOD: Dynamic discovery
if (codespaceName != null)
{
    testsite.WithEnvironment("KEYCLOAK_BACKCHANNEL_URL", keycloak.GetEndpoint("http"));
    testsite.WithEnvironment("BUSINESSAPP_BACKCHANNEL_URL", businessApp.GetEndpoint("http"));
}

// ❌ BAD: Hardcoded ports
if (codespaceName != null)
{
    testsite.WithEnvironment("KEYCLOAK_BACKCHANNEL_URL", "http://localhost:8080");
    testsite.WithEnvironment("BUSINESSAPP_BACKCHANNEL_URL", "http://localhost:5163");
}
```

## Why GetEndpoint("http") Works

**For containers** (like Keycloak):
- `GetEndpoint("http")` returns `http://localhost:{assigned-port}`
- Works reliably because containers explicitly define endpoint mappings

**For projects** (like MockBusinessApp):
- HTTP endpoint returns plain `http://localhost:{port}` URL (not a service discovery URL)
- Works from plain HttpClient without Aspire service discovery extensions
- HTTPS endpoints may return service discovery URLs that don't resolve from plain HttpClient

**Historical note**: An earlier attempt used `businessApp.GetEndpoint("https")` and failed because it returned a service discovery URL. The HTTP endpoint avoids this issue.

## When to Use This

**Use dynamic discovery when:**
- Setting backchannel URLs for server-to-server calls in Codespaces
- The endpoint might have ephemeral port assignment
- Consistency with other backchannel patterns matters

**Don't use this when:**
- Setting browser-facing public URLs (use Codespaces URL derivation instead)
- The port is guaranteed stable (but prefer dynamic discovery anyway for consistency)

## Test Contract

Validate the dynamic discovery pattern in tests:

```csharp
[Fact]
public void AppHost_ConfiguresBackchannelWithDynamicDiscovery()
{
    var program = File.ReadAllText(Path.Combine(RepoRoot, "src/UmbracoPrism.AppHost/Program.cs"));
    
    program.Should().Contain(".WithEnvironment(\"BUSINESSAPP_BACKCHANNEL_URL\", businessApp.GetEndpoint(\"http\"))",
        because: "Aspire's dynamic endpoint discovery ensures the correct HTTP port is used, " +
                 "avoiding hardcoded ports that may differ across environments or Aspire configurations");
}
```

## Examples

**AppHost/Program.cs:**
```csharp
// Container resource — dynamic discovery for backchannel
var keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "26.0.0")
    .WithHttpEndpoint(port: null, targetPort: 8080, name: "http");

if (codespaceName != null)
    testsite.WithEnvironment("KEYCLOAK_BACKCHANNEL_URL", keycloak.GetEndpoint("http"));

// Project resource — dynamic discovery for backchannel
var businessApp = builder.AddProject("businessapp", "../UmbracoPrism.MockBusinessApp/UmbracoPrism.MockBusinessApp.csproj", launchProfileName: "https");

if (codespaceName != null)
    testsite.WithEnvironment("BUSINESSAPP_BACKCHANNEL_URL", businessApp.GetEndpoint("http"));
```

**Controller usage:**
```csharp
private string ResolveBusinessAppTransportBaseUrl()
{
    // Check for backchannel URL first (dynamic in Codespaces)
    var backchannelUrl = Environment.GetEnvironmentVariable("BUSINESSAPP_BACKCHANNEL_URL")?.TrimEnd('/');
    if (!string.IsNullOrWhiteSpace(backchannelUrl))
        return backchannelUrl;
    
    // Fall back to configured public URL (for local dev without backchannel)
    var baseUrl = configuration["PrismBusinessApp:WorkflowApiBaseUrl"]?.TrimEnd('/');
    if (string.IsNullOrWhiteSpace(baseUrl))
        throw new InvalidOperationException("PrismBusinessApp:WorkflowApiBaseUrl is not configured.");
    
    return baseUrl;
}
```

## Anti-Patterns

- **Hardcoding backchannel ports** — breaks when Aspire assigns different ports
- **Using GetEndpoint("https") for plain HttpClient** — returns service discovery URLs that don't resolve
- **Assuming launchSettings.json ports are stable** — Aspire's runtime assignment takes precedence

## Diagnosis Steps

When backchannel calls timeout:

1. **Check if the endpoint is listening:**
   ```bash
   curl -v http://localhost:5163/api/backoffice/me
   ```
   
2. **Check Aspire's assigned ports:**
   Look at Aspire dashboard or logs to see what ports were actually assigned

3. **Verify GetEndpoint output:**
   Add temporary logging in AppHost:
   ```csharp
   var endpoint = businessApp.GetEndpoint("http");
   Console.WriteLine($"[PRISM] BusinessApp backchannel: {endpoint}");
   ```

4. **Confirm plain HttpClient can reach it:**
   ```bash
   curl http://localhost:{discovered-port}/api/backoffice/me
   ```

## Related Skills

- **aspire-project-endpoint-profiles**: Configuring launch profiles for stable browser-facing URLs
- **codespaces-url-forms**: Deriving public Codespaces URLs for browser-facing endpoints
- **backchannel-rewrite-testing**: Testing backchannel URL transformation in browser-facing responses

## References

- Implementation: PR #49 (commit `2a46494`)
- Failed HTTPS attempt: Commit `ffc32c5` (removed `businessApp.GetEndpoint("https")`)
- Test contract: `DashboardLocalEndpointsValidationTests.AppHost_ConfiguresBusinessAppBackchannel_ForCodespacesServerCalls`
- Decision: `.squad/decisions/inbox/blathers-backchannel-dynamic-discovery.md`
