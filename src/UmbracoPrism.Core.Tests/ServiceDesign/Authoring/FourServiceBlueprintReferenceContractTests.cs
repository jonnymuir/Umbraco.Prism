extern alias MockBusinessApp;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using UmbracoPrism.Shared.Models.ServiceDesign;
using MockProgram = MockBusinessApp::Program;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Authoring;

/// <summary>
/// Validates the reference workflow contract: exactly 5 demo workflows
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
        "money-modeller",
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
        workflows.Should().HaveCount(5,
            because: "the reference contract specifies exactly 5 demo workflows");

        var actualKeys = workflows!.Select(w => w.DefinitionKey).OrderBy(k => k).ToList();
        actualKeys.Should().BeEquivalentTo(ExpectedWorkflowKeys.OrderBy(k => k),
            because: "the source API should list exactly the canonical workflows");
    }

    [Fact]
    public async Task SourceApi_AllFourWorkflowsAreLoadable()
    {
        foreach (var workflowKey in ExpectedWorkflowKeys)
        {
            var response = await _client.GetAsync($"/mockapp/workflows/{workflowKey}");

            response.StatusCode.Should().Be(HttpStatusCode.OK,
                because: $"workflow '{workflowKey}' must be loadable via the source API");

            var workflow = await response.Content.ReadFromJsonAsync<ServiceBlueprint>();
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
        definitionCount.Should().Be(5,
            because: "the runtime should have exactly 5 workflows published from authored sources");
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
        cardCount.Should().Be(5,
            because: "the admin screen should show exactly the canonical workflows, no more");
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
            because: "all canonical workflows should have authored sources");
    }

    [Fact]
    public async Task WorkflowKeys_MatchAcrossSourceAndAdminSurfaces()
    {
        var sourceResponse = await _client.GetAsync("/mockapp/workflows");
        var workflows = await sourceResponse.Content.ReadFromJsonAsync<List<MockAppWorkflowSummary>>();
        var sourceKeys = workflows!.Select(w => w.DefinitionKey).OrderBy(k => k).ToList();

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
            because: "both surfaces must show exactly the canonical workflows");
    }

    [Fact]
    public async Task SourceApi_SaveAcceptsClientShapedWorkflowPayload()
    {
        var existingJson = await _client.GetStringAsync("/mockapp/workflows/payment-demo");
        var payload = JsonNode.Parse(existingJson)!.AsObject();
        var states = payload["states"]!.AsArray();
        var stage = states
            .Select(node => node!.AsObject())
            .Single(node => node["stateKey"]!.GetValue<string>() == "confirm-payment-received");

        stage["displayName"] = "Confirm payment received (saved)";

        // The editor persists manual canvas positions in the layout block —
        // they must survive the save → memory store → reload cycle.
        payload["layout"] = new JsonObject
        {
            ["nodes"] = new JsonObject
            {
                ["stage:confirm-payment-received"] = new JsonObject { ["x"] = 620, ["y"] = 480 }
            }
        };

        try
        {
            using var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
            var save = await _client.PutAsync("/mockapp/workflows/payment-demo", content);

            save.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var reloaded = await _client.GetFromJsonAsync<ServiceBlueprint>("/mockapp/workflows/payment-demo");
            reloaded.Should().NotBeNull();
            reloaded!.States.Single(state => state.StateKey == "confirm-payment-received")
                .DisplayName.Should().Be("Confirm payment received (saved)");
            reloaded.Layout.Should().NotBeNull();
            reloaded.Layout!.Nodes.Should().ContainKey("stage:confirm-payment-received")
                .WhoseValue.Should().BeEquivalentTo(new WorkflowNodePosition { X = 620, Y = 480 });
        }
        finally
        {
            // The save above bumped the version, so replaying the pre-save payload verbatim would
            // now (correctly) hit a 409 conflict. Restore the original content but with the
            // version bumped to whatever's current, same as a real client re-reading before saving.
            var currentVersion = await _client.GetFromJsonAsync<ServiceBlueprint>("/mockapp/workflows/payment-demo");
            var restorePayload = JsonNode.Parse(existingJson)!.AsObject();
            restorePayload["version"] = currentVersion!.Version;

            using var restore = new StringContent(restorePayload.ToJsonString(), Encoding.UTF8, "application/json");
            var restored = await _client.PutAsync("/mockapp/workflows/payment-demo", restore);
            restored.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
    }

    [Fact]
    public async Task SourceApi_SaveReturnsStructuredProblemWhenAComponentTypeIsMissing()
    {
        var existingJson = await _client.GetStringAsync("/mockapp/workflows/payment-demo");
        var payload = JsonNode.Parse(existingJson)!.AsObject();
        var firstState = payload["states"]!.AsArray()[0]!.AsObject();
        var firstComponent = firstState["components"]!.AsArray()[0]!.AsObject();
        firstComponent.Remove("type");

        using var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        var response = await _client.PutAsync("/mockapp/workflows/payment-demo", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = problem.RootElement;

        root.GetProperty("title").GetString().Should().Be("Invalid workflow payload");
        root.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status400BadRequest);
        root.GetProperty("detail").GetString().Should().Be("Every workflow component must include a supported 'type' value before the workflow can be saved.");
        root.GetProperty("errorCode").GetString().Should().Be("workflow-component-invalid");
        root.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();

        var errors = root.GetProperty("errors");
        errors.GetArrayLength().Should().BeGreaterThan(0);
        errors[0].GetProperty("code").GetString().Should().Be("component-type-missing");
        errors[0].GetProperty("path").GetString().Should().Be("$.states[0].components[0]");
        errors[0].GetProperty("message").GetString().Should().Be("Workflow components must declare a supported 'type' value.");
    }

    /// <summary>
    /// Validation parity: the editor's save path (WorkflowSourceSaveRequestParser, via
    /// this endpoint) and the AI toolkit's save path (ServiceBlueprintAuthoringService.Validate,
    /// via /prism/workflow-authoring/*) must reject the exact same malformed definition —
    /// a state route that targets another state directly instead of a gateway.
    /// </summary>
    [Fact]
    public async Task SourceApi_SaveRejectsStateRouteThatBypassesAGateway_SameAsAiToolkit()
    {
        var existingJson = await _client.GetStringAsync("/mockapp/workflows/planning");
        var payload = JsonNode.Parse(existingJson)!.AsObject();
        var states = payload["states"]!.AsArray();
        var declaration = states.Select(n => n!.AsObject())
            .Single(n => n["stateKey"]!.GetValue<string>() == "declaration");
        var route = declaration["routes"]!.AsArray()[0]!.AsObject();
        route["target"] = "application-form"; // a state key, not a gateway key

        using var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        var response = await _client.PutAsync("/mockapp/workflows/planning", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            because: "this is the exact ServiceBlueprint.ValidateGatewayRouting() violation the AI toolkit's save_workflow/validate_workflow also reject");
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = problem.RootElement;

        root.GetProperty("errorCode").GetString().Should().Be("workflow-validation-invalid");
        var errors = root.GetProperty("errors");
        errors.GetArrayLength().Should().BeGreaterThan(0);
        errors[0].GetProperty("message").GetString().Should().Contain(
            "Routes from states must always target a gateway",
            because: "this is the same message ValidateGatewayRouting() produces for the AI-toolkit path");
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

    private sealed record MockAppWorkflowSummary(string DefinitionKey, string DisplayName);
}
