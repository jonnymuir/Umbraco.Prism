extern alias MockBusinessApp;

using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using UmbracoPrism.WorkflowEditor.Authoring;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

/// <summary>
/// Regression tests for Slice 8a — relaxed <see cref="ProposalEnvelope"/> shape
/// and consolidated write surface. Pins:
///   1. <c>POST /apply</c> with no Agent and no Rationale succeeds and the persisted
///      provenance synthesises an agent from the authenticated principal.
///   2. <c>POST /apply</c> with an arbitrary actor string in <c>agent.kind</c>
///      (i.e. not one of the historical github-copilot / custom-agent /
///      human-assisted labels) is accepted — Kind is a free-form string.
///   3. <c>POST /apply</c> with no operations returns 400 — envelope mode must
///      carry at least one op; whole-document saves belong on /publish.
///   4. <c>POST /save</c> on a real workflow returns 404 — the alias is retired.
/// </summary>
[Collection("WorkflowAuthoringFactory")]
public class WorkflowAuthoringApplyRelaxationTests
{
    private readonly WorkflowAuthoringWebFactory _factory;

    public WorkflowAuthoringApplyRelaxationTests(WorkflowAuthoringWebFactory factory)
    {
        _factory = factory;
    }

    private static object NoOpUpdateTransitionOp() => new
    {
        op = "update-transition",
        value = new { source = "declaration", target = "route-application-form", trigger = "continue" }
    };

    [Fact]
    public async Task PostApply_WithoutAgentOrRationale_SucceedsAndSynthesisesAgentFromPrincipal()
    {
        using var client = _factory.CreateAuthenticatedClient("alice");

        // No agent. No rationale. Just an id, a created-at stamp, the target workflow
        // and at least one op. This is the shape an integrator can realistically write
        // for a non-agentic save.
        var body = JsonSerializer.Serialize(new
        {
            envelope = new
            {
                id = Guid.NewGuid(),
                createdAt = DateTimeOffset.UtcNow,
                targetWorkflowId = "planning-application",
                ops = new[] { NoOpUpdateTransitionOp() }
            }
        }, WorkflowProjector.CanonicalOptions);

        var response = await client.PostAsync(
            "/api/workflow-authoring/workflows/planning/apply",
            new StringContent(body, Encoding.UTF8, "application/json"));

        var responseBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: responseBody);

        using var doc = JsonDocument.Parse(responseBody);
        doc.RootElement.TryGetProperty("provenancePath", out var provenancePathEl).Should().BeTrue();
        var provenancePath = provenancePathEl.GetString();
        provenancePath.Should().NotBeNullOrWhiteSpace();

        var provenanceJson = await File.ReadAllTextAsync(provenancePath!);
        provenanceJson.Should().Contain("\"approver\":\"alice\"".Replace("\"", "\""),
            because: "the synthesised agent is derived from the authenticated principal");
        provenanceJson.Should().MatchRegex("\"kind\"\\s*:\\s*\"human-assisted\"",
            because: "absent an explicit agent the endpoint stamps a human-assisted actor");
        provenanceJson.Should().MatchRegex("\"identity\"\\s*:\\s*\"alice\"",
            because: "the synthesised agent identity must be the calling principal");
    }

    [Fact]
    public async Task PostApply_WithCustomActorStringKind_IsAccepted()
    {
        using var client = _factory.CreateAuthenticatedClient("alice");

        // 'planning-bot' is not one of the historical kinds (github-copilot,
        // custom-agent, human-assisted). Slice 8a makes Kind a free-form string,
        // so this must be accepted and recorded as-is.
        const string customKind = "planning-bot";
        var body = JsonSerializer.Serialize(new
        {
            envelope = new
            {
                id = Guid.NewGuid(),
                createdAt = DateTimeOffset.UtcNow,
                agent = new { kind = customKind, identity = "bot@example.com" },
                targetWorkflowId = "planning-application",
                ops = new[] { NoOpUpdateTransitionOp() }
            }
        }, WorkflowProjector.CanonicalOptions);

        var response = await client.PostAsync(
            "/api/workflow-authoring/workflows/planning/apply",
            new StringContent(body, Encoding.UTF8, "application/json"));

        var responseBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: responseBody);

        using var doc = JsonDocument.Parse(responseBody);
        var provenancePath = doc.RootElement.GetProperty("provenancePath").GetString();
        provenancePath.Should().NotBeNullOrWhiteSpace();

        var provenanceJson = await File.ReadAllTextAsync(provenancePath!);
        provenanceJson.Should().MatchRegex($"\"kind\"\\s*:\\s*\"{customKind}\"",
            because: "the custom actor kind must be recorded verbatim in provenance");
    }

    [Fact]
    public async Task PostApply_WithEmptyOps_ReturnsBadRequest()
    {
        using var client = _factory.CreateAuthenticatedClient("alice");

        var body = JsonSerializer.Serialize(new
        {
            envelope = new
            {
                id = Guid.NewGuid(),
                createdAt = DateTimeOffset.UtcNow,
                targetWorkflowId = "planning-application",
                ops = Array.Empty<object>()
            }
        }, WorkflowProjector.CanonicalOptions);

        var response = await client.PostAsync(
            "/api/workflow-authoring/workflows/planning/apply",
            new StringContent(body, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(
            HttpStatusCode.BadRequest,
            because: "envelope mode exists to carry ops; an empty envelope belongs on /publish");
    }

    [Fact]
    public async Task PostSave_OnExistingWorkflow_ReturnsNotFound_BecauseAliasWasRetired()
    {
        using var client = _factory.CreateAuthenticatedClient("alice");

        var response = await client.PostAsync(
            "/api/workflow-authoring/workflows/planning/save",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            because: "Slice 8a removed the /save alias; integrators target /publish or /apply");
    }
}
