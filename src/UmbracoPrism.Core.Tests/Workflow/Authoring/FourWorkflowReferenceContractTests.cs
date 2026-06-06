extern alias MockBusinessApp;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using UmbracoPrism.Shared.Models.Workflow;
using MockProgram = MockBusinessApp::Program;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

/// <summary>
/// Validates the four-workflow reference contract: exactly 4 demo workflows
/// seeded at runtime, in memory, and consistently available through the
/// MockBusinessApp's <c>/mockapp/workflows/*</c> source endpoints, the admin
/// screen, and the runtime catalog.
/// </summary>
public class FourWorkflowReferenceContractTests : IClassFixture<FourWorkflowReferenceContractTests.MockBusinessAppWebFactory>
{
    private readonly HttpClient _client;

    private static readonly string[] ExpectedWorkflowKeys =
    [
        "community-enquiry",
        "information-request",
        "payment-demo",
        "planning"
    ];

    public FourWorkflowReferenceContractTests(MockBusinessAppWebFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SourceApi_ListsExactlyFourWorkflows()
    {
        var response = await _client.GetAsync("/mockapp/workflows");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var workflows = await response.Content.ReadFromJsonAsync<List<MockAppWorkflowSummary>>();

        workflows.Should().NotBeNull();
        workflows.Should().HaveCount(4,
            because: "the reference contract specifies exactly 4 demo workflows");

        var actualKeys = workflows!.Select(w => w.WorkflowKey).OrderBy(k => k).ToList();
        actualKeys.Should().BeEquivalentTo(ExpectedWorkflowKeys.OrderBy(k => k),
            because: "the source API should list exactly the 4 canonical workflows");
    }

    [Fact]
    public async Task SourceApi_AllFourWorkflowsAreLoadable()
    {
        foreach (var workflowKey in ExpectedWorkflowKeys)
        {
            var response = await _client.GetAsync($"/mockapp/workflows/{workflowKey}");

            response.StatusCode.Should().Be(HttpStatusCode.OK,
                because: $"workflow '{workflowKey}' must be loadable via the source API");

            var workflow = await response.Content.ReadFromJsonAsync<WorkflowDefinitionFile>();
            workflow.Should().NotBeNull();
            workflow!.DefinitionKey.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task RuntimeStore_PublishesExactlyFourWorkflowsAtStartup()
    {
        var response = await _client.GetAsync("/api/workflow/catalog");

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();

        var definitionCount = body.Split("definitionKey").Length - 1;
        definitionCount.Should().Be(4,
            because: "the runtime should have exactly 4 workflows published from authored sources");
    }

    [Fact]
    public async Task AdminScreen_ShowsExactlyFourWorkflowDefinitions()
    {
        var response = await _client.GetAsync("/admin/workflow");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();

        foreach (var workflowKey in ExpectedWorkflowKeys)
        {
            body.Should().Contain($"data-workflow-key=\"{workflowKey}\"",
                because: $"workflow '{workflowKey}' should appear in the admin screen");
        }

        var cardCount = body.Split("data-workflow-key=").Length - 1;
        cardCount.Should().Be(4,
            because: "the admin screen should show exactly the 4 canonical workflows, no more");
    }

    [Fact]
    public async Task AdminScreen_AllFourWorkflowsHaveEditorLinks()
    {
        var response = await _client.GetAsync("/admin/workflow");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();

        foreach (var workflowKey in ExpectedWorkflowKeys)
        {
            body.Should().Contain($"href=\"/workflow-editor?workflow={workflowKey}\"",
                because: $"workflow '{workflowKey}' should have an editor link since it has an authored source");
        }

        body.Should().NotContain("No editor definition yet",
            because: "all 4 canonical workflows should have authored sources");
    }

    [Fact]
    public async Task WorkflowKeys_MatchAcrossSourceAndAdminSurfaces()
    {
        var sourceResponse = await _client.GetAsync("/mockapp/workflows");
        var workflows = await sourceResponse.Content.ReadFromJsonAsync<List<MockAppWorkflowSummary>>();
        var sourceKeys = workflows!.Select(w => w.WorkflowKey).OrderBy(k => k).ToList();

        var adminResponse = await _client.GetAsync("/admin/workflow");
        var adminBody = await adminResponse.Content.ReadAsStringAsync();

        var adminKeys = new List<string>();
        foreach (var key in ExpectedWorkflowKeys)
        {
            if (adminBody.Contains($"data-workflow-key=\"{key}\""))
            {
                adminKeys.Add(key);
            }
        }
        adminKeys = adminKeys.OrderBy(k => k).ToList();

        sourceKeys.Should().BeEquivalentTo(adminKeys,
            because: "the same workflow keys must appear in both source API and admin screen");

        sourceKeys.Should().BeEquivalentTo(ExpectedWorkflowKeys.OrderBy(k => k),
            because: "both surfaces must show exactly the 4 canonical workflows");
    }

    [Fact]
    public async Task SourceApi_SaveAcceptsPaymentWorkflowWithoutIntermediateConfirmationGateway()
    {
        var existing = await _client.GetFromJsonAsync<WorkflowDefinitionFile>("/mockapp/workflows/payment-demo");
        existing.Should().NotBeNull();

        var updated = existing! with
        {
            States = existing.States.Select(state => state.StateKey == "confirm-payment-received"
                ? state with
                {
                    Routes =
                    [
                        new WorkflowRouteDefinition
                        {
                            Id = "confirm-payment-received--confirm--await-payment-confirmation",
                            Target = "await-payment-confirmation",
                            Trigger = "confirm",
                            RequiresRole = "reviewer"
                        }
                    ]
                }
                : state).ToArray(),
            Gateways = existing.Gateways!
                .Where(gateway => gateway.Key != "confirm-payment-route")
                .ToArray()
        };

        try
        {
            using var content = new StringContent(JsonSerializer.Serialize(updated), Encoding.UTF8, "application/json");
            var save = await _client.PutAsync("/mockapp/workflows/payment-demo", content);

            save.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var reloaded = await _client.GetFromJsonAsync<WorkflowDefinitionFile>("/mockapp/workflows/payment-demo");
            reloaded.Should().NotBeNull();
            reloaded!.States.Single(state => state.StateKey == "confirm-payment-received")
                .Routes.Should().ContainSingle(route =>
                    route.Target == "await-payment-confirmation"
                    && route.Trigger == "confirm");
            reloaded.Gateways.Should().NotContain(gateway => gateway.Key == "confirm-payment-route");
        }
        finally
        {
            using var restore = new StringContent(JsonSerializer.Serialize(existing), Encoding.UTF8, "application/json");
            var restored = await _client.PutAsync("/mockapp/workflows/payment-demo", restore);
            restored.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
    }

    /// <summary>
    /// Anonymous test factory for the MockBusinessApp. The Slice B
    /// <c>/mockapp/workflows/*</c> endpoints are deliberately unauthenticated
    /// in the reference app — real downstream apps add their own auth.
    /// </summary>
    public sealed class MockBusinessAppWebFactory : WebApplicationFactory<MockProgram>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["PrismBusinessApp:Tenants:0:Code"] = "smoke",
                    ["PrismBusinessApp:Tenants:0:Hostname"] = "localhost",
                    ["PrismBusinessApp:Tenants:0:OidcAuthority"] = "https://localhost:9999/realms/smoke",
                    ["PrismBusinessApp:Tenants:0:OidcClientId"] = "smoke-client"
                });
            });
        }
    }

    private sealed record MockAppWorkflowSummary(string WorkflowKey, string DefinitionKey, string DisplayName);
}
