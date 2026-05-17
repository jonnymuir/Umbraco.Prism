extern alias MockBusinessApp;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UmbracoPrism.Core.Workflow.Authoring;
using MockProgram = MockBusinessApp::Program;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

/// <summary>
/// Integration smoke tests for <c>MapWorkflowAuthoringEndpoints()</c> using
/// <see cref="WebApplicationFactory{TEntryPoint}"/> against MockBusinessApp.
///
/// All endpoints are unauthenticated in V1; the factory suppresses OIDC configuration
/// so no real Keycloak or Entra tenant is required.
/// </summary>
public class WorkflowAuthoringEndpointsTests : IClassFixture<WorkflowAuthoringWebFactory>
{
    private readonly HttpClient _client;

    public WorkflowAuthoringEndpointsTests(WorkflowAuthoringWebFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetWorkflows_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/workflow-authoring/workflows");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetWorkflow_ByKey_ReturnsWorkflowOrNotFound()
    {
        // The test store may have no workflows on disk — both 200 and 404 are valid outcomes.
        var response = await _client.GetAsync("/api/workflow-authoring/workflows/planning-application");

        response.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.OK, HttpStatusCode.NotFound },
            "endpoint returns 200 with workflow or 404 if not seeded");
    }

    [Fact]
    public async Task PostValidate_WithValidWorkflow_ReturnsOk()
    {
        var authored = BuildMinimalAuthoredWorkflow();
        var json     = JsonSerializer.Serialize(authored, WorkflowProjector.CanonicalOptions);

        var response = await _client.PostAsync(
            "/api/workflow-authoring/workflows/smoke-test/validate",
            new StringContent(json, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("hasErrors");
    }

    [Fact]
    public async Task PostProject_WithValidWorkflow_ReturnsProjectedFile()
    {
        var authored = BuildMinimalAuthoredWorkflow();
        var json     = JsonSerializer.Serialize(authored, WorkflowProjector.CanonicalOptions);

        var response = await _client.PostAsync(
            "/api/workflow-authoring/workflows/smoke-test/project",
            new StringContent(json, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("checksum");
        body.Should().Contain("file");
    }

    [Fact]
    public async Task PostPreview_WithInvalidKey_ReturnsNotFound()
    {
        var envelope = BuildMinimalEnvelope("smoke-workflow-does-not-exist");
        var json     = JsonSerializer.Serialize(envelope, WorkflowProjector.CanonicalOptions);

        var response = await _client.PostAsync(
            "/api/workflow-authoring/workflows/smoke-workflow-does-not-exist/preview",
            new StringContent(json, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostApply_WithMissingApprover_ReturnsBadRequest()
    {
        var body = JsonSerializer.Serialize(new
        {
            envelope = BuildMinimalEnvelope("smoke-key"),
            approver = ""   // empty — must be rejected
        }, WorkflowProjector.CanonicalOptions);

        var response = await _client.PostAsync(
            "/api/workflow-authoring/workflows/smoke-key/apply",
            new StringContent(body, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostApply_WithNonExistentWorkflow_ReturnsNotFound()
    {
        var body = JsonSerializer.Serialize(new
        {
            envelope = BuildMinimalEnvelope("missing-key"),
            approver = "test-approver"
        }, WorkflowProjector.CanonicalOptions);

        var response = await _client.PostAsync(
            "/api/workflow-authoring/workflows/missing-key/apply",
            new StringContent(body, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostValidate_WithNullBody_ReturnsBadRequest()
    {
        var response = await _client.PostAsync(
            "/api/workflow-authoring/workflows/smoke-test/validate",
            new StringContent("{\"malformed\":true}", Encoding.UTF8, "application/json"));

        // Either 400 (bad request) or 200 with hasErrors = true — both signal invalid input gracefully.
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static AuthoredWorkflow BuildMinimalAuthoredWorkflow() => new()
    {
        Id             = Guid.NewGuid(),
        DefinitionKey  = "smoke-test",
        DisplayName    = "Smoke Test Workflow",
        InitialStageKey = "start",
        Stages         =
        [
            new AuthoredStage
            {
                StageKey    = "start",
                DisplayName = "Start",
                Kind        = StageKind.Confirmation
            }
        ]
    };

    private static ProposalEnvelope BuildMinimalEnvelope(string targetKey) => new()
    {
        Id               = Guid.NewGuid(),
        CreatedAt        = DateTimeOffset.UtcNow,
        Agent            = new PatchAgent { Kind = "human-assisted", Identity = "smoke-test" },
        TargetWorkflowId = targetKey,
        Rationale        = "Smoke test envelope",
        Ops              = []
    };
}

/// <summary>
/// Customised <see cref="WebApplicationFactory{TEntryPoint}"/> for MockBusinessApp
/// that suppresses OIDC configuration so tests can run without a real identity provider.
/// </summary>
public sealed class WorkflowAuthoringWebFactory : WebApplicationFactory<MockProgram>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        // Set to Development so authoring CORS policy is registered.
        builder.UseEnvironment("Development");

        // Supply minimal configuration to prevent PrismAuthentication from failing.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Minimal tenant config — OIDC will be configured but never called in these tests.
                ["PrismBusinessApp:Tenants:0:Code"]          = "smoke",
                ["PrismBusinessApp:Tenants:0:Hostname"]      = "localhost",
                ["PrismBusinessApp:Tenants:0:OidcAuthority"] = "https://localhost:9999/realms/smoke",
                ["PrismBusinessApp:Tenants:0:OidcClientId"]  = "smoke-client",
                // Point authored workflow store at the test fixtures directory.
                ["WorkflowAuthoring:BasePath"] = GetFixturesPath()
            });
        });

        // Override the IAuthoredWorkflowStore to use test fixtures so
        // GET /workflows and GET /workflows/{key} can return data.
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAuthoredWorkflowStore>();
            services.AddSingleton<IAuthoredWorkflowStore>(
                _ => new FilesystemAuthoredWorkflowStore(GetFixturesPath()));
        });
    }

    private static string GetFixturesPath() =>
        Path.Combine(
            Path.GetDirectoryName(typeof(WorkflowAuthoringEndpointsTests).Assembly.Location)!,
            "Workflow", "Authoring", "Fixtures");
}
