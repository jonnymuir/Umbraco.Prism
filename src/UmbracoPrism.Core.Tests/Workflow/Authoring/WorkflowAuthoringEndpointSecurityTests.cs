extern alias MockBusinessApp;

using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using UmbracoPrism.WorkflowEditor.Authoring;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

/// <summary>
/// Regression tests for the security hardening of <c>/api/workflow-authoring/*</c>
/// (Slice 3c). These pin Copper's verification matrix:
///   1. unauthenticated callers hit 401 on save/publish/apply,
///   2. path-traversal keys are rejected at the endpoint AND inside the filesystem stores,
///   3. the persisted approver is the authenticated principal, never the request body,
///   4. human-assisted envelopes whose agent identity does not match the caller are rejected.
/// </summary>
[Collection("WorkflowAuthoringFactory")]
public class WorkflowAuthoringEndpointSecurityTests
{
    private readonly WorkflowAuthoringWebFactory _factory;

    public WorkflowAuthoringEndpointSecurityTests(WorkflowAuthoringWebFactory factory)
    {
        _factory = factory;
    }

    // ─── Authentication ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("/api/workflow-authoring/workflows/smoke-test/save")]
    [InlineData("/api/workflow-authoring/workflows/smoke-test/publish")]
    [InlineData("/api/workflow-authoring/workflows/smoke-test/apply")]
    public async Task UnauthenticatedRequest_ReturnsUnauthorized(string path)
    {
        using var client = _factory.CreateClient(); // no X-Test-User header

        var response = await client.PostAsync(
            path,
            new StringContent("{}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(
            HttpStatusCode.Unauthorized,
            because: "every /api/workflow-authoring/* route must require an authenticated principal");
    }

    // ─── Path traversal (endpoint layer) ─────────────────────────────────────

    [Theory]
    [InlineData("../evil")]
    [InlineData("..%2Fevil")]
    [InlineData("foo/bar")]
    [InlineData("foo.bar")]
    [InlineData("with space")]
    public async Task PostSave_WithUnsafeKey_ReturnsBadRequest_AndDoesNotWriteOutsideBasePath(string unsafeKey)
    {
        using var client = _factory.CreateAuthenticatedClient("alice");
        var encodedKey = Uri.EscapeDataString(unsafeKey);

        var response = await client.PostAsync(
            $"/api/workflow-authoring/workflows/{encodedKey}/save",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(
            HttpStatusCode.BadRequest,
            because: $"key '{unsafeKey}' fails the ^[a-zA-Z0-9_-]+$ guard at the endpoint layer");

        File.Exists(Path.Combine(Path.GetTempPath(), $"{unsafeKey}.workflow.json"))
            .Should().BeFalse(because: "no file should ever land outside the configured base directory");
    }

    [Fact]
    public async Task PostApply_WithUnsafeKey_ReturnsBadRequest()
    {
        using var client = _factory.CreateAuthenticatedClient("alice");

        var response = await client.PostAsync(
            "/api/workflow-authoring/workflows/" + Uri.EscapeDataString("../evil") + "/apply",
            new StringContent("{\"envelope\":{}}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostPublish_WithUnsafeKey_ReturnsBadRequest()
    {
        using var client = _factory.CreateAuthenticatedClient("alice");

        var response = await client.PostAsync(
            "/api/workflow-authoring/workflows/" + Uri.EscapeDataString("../evil") + "/publish",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─── Path traversal (defence in depth: the stores themselves refuse) ─────

    [Fact]
    public void FilesystemAuthoredStore_LoadAsync_WithTraversalKey_Throws()
    {
        var basePath = Path.Combine(Path.GetTempPath(), $"prism-authored-{Guid.NewGuid():N}");
        Directory.CreateDirectory(basePath);
        try
        {
            var store = new FilesystemAuthoredWorkflowStore(basePath);

            var act = () => store.LoadAsync("../escape").GetAwaiter().GetResult();

            act.Should().Throw<InvalidOperationException>(
                because: "the store must enforce containment even if a caller bypasses the endpoint sanitiser");
        }
        finally
        {
            if (Directory.Exists(basePath)) Directory.Delete(basePath, recursive: true);
        }
    }

    [Fact]
    public void FilesystemAuthoredStore_SaveAsync_WithTraversalKey_Throws_AndWritesNoFile()
    {
        var basePath = Path.Combine(Path.GetTempPath(), $"prism-authored-{Guid.NewGuid():N}");
        Directory.CreateDirectory(basePath);
        var parent = Directory.GetParent(basePath)!.FullName;
        var sentinel = Path.Combine(parent, "..escape.workflow.json");
        try
        {
            var store = new FilesystemAuthoredWorkflowStore(basePath);
            var authored = new AuthoredWorkflow
            {
                Id = Guid.NewGuid(),
                DefinitionKey = "smoke",
                DisplayName = "Smoke",
                InitialStageKey = "start",
                Stages = []
            };

            var act = () => store.SaveAsync("../escape", authored).GetAwaiter().GetResult();

            act.Should().Throw<InvalidOperationException>();
            File.Exists(sentinel).Should().BeFalse(
                because: "the path-guard must refuse to open the file rather than silently writing outside basePath");
        }
        finally
        {
            if (File.Exists(sentinel)) File.Delete(sentinel);
            if (Directory.Exists(basePath)) Directory.Delete(basePath, recursive: true);
        }
    }

    [Fact]
    public void FilesystemPublishedStore_SaveAsync_WithTraversalDefinitionKey_Throws()
    {
        var basePath = Path.Combine(Path.GetTempPath(), $"prism-published-{Guid.NewGuid():N}");
        Directory.CreateDirectory(basePath);
        try
        {
            var store = new FilesystemPublishedWorkflowStore(basePath);
            var file = new UmbracoPrism.Shared.Models.Workflow.WorkflowDefinitionFile
            {
                DefinitionKey = "../escape",
                DisplayName = "Escape",
                InitialState = "start"
            };

            var act = () => store.SaveAsync(file).GetAwaiter().GetResult();

            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            if (Directory.Exists(basePath)) Directory.Delete(basePath, recursive: true);
        }
    }

    [Fact]
    public void FilesystemProvenanceStore_SaveAsync_WithTraversalWorkflowKey_Throws()
    {
        var basePath = Path.Combine(Path.GetTempPath(), $"prism-provenance-{Guid.NewGuid():N}");
        Directory.CreateDirectory(basePath);
        try
        {
            var store = new FilesystemWorkflowAuthoringProvenanceStore(basePath);
            var envelope = new ProposalEnvelope
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTimeOffset.UtcNow,
                Agent = new PatchAgent { Kind = "human-assisted", Identity = "alice" },
                TargetWorkflowId = "smoke",
                Rationale = "test"
            };

            var act = () => store.SaveAsync("../escape", envelope, "alice").GetAwaiter().GetResult();

            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            if (Directory.Exists(basePath)) Directory.Delete(basePath, recursive: true);
        }
    }

    // ─── Approver from claims, not body ──────────────────────────────────────

    [Fact]
    public async Task PostApply_WithBodyApproverField_IgnoresBodyAndUsesAuthenticatedPrincipal()
    {
        using var client = _factory.CreateAuthenticatedClient("alice");

        // Note the bogus "approver: bob" — after the DTO removal it has no effect.
        // The persisted provenance must name "alice".
        var body = JsonSerializer.Serialize(new
        {
            envelope = new
            {
                id = Guid.NewGuid(),
                createdAt = DateTimeOffset.UtcNow,
                agent = new { kind = "human-assisted", identity = "alice" },
                targetWorkflowId = "planning-application",
                rationale = "Slice 3c: approver-from-claims regression"
            },
            approver = "bob"
        }, WorkflowProjector.CanonicalOptions);

        var response = await client.PostAsync(
            "/api/workflow-authoring/workflows/planning/apply",
            new StringContent(body, Encoding.UTF8, "application/json"));

        var responseBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: responseBody);

        // Pull the provenance file path back out of the response and verify its contents.
        using var doc = JsonDocument.Parse(responseBody);
        doc.RootElement.TryGetProperty("provenancePath", out var provenancePathEl).Should().BeTrue();
        var provenancePath = provenancePathEl.GetString();
        provenancePath.Should().NotBeNullOrWhiteSpace(
            because: "apply must persist a provenance record naming the authenticated approver");

        var provenanceJson = await File.ReadAllTextAsync(provenancePath!);
        var hasAlice = provenanceJson.Contains("\"approver\":\"alice\"")
            || provenanceJson.Contains("\"approver\": \"alice\"");
        hasAlice.Should().BeTrue(
            because: "the persisted approver must be the authenticated principal ('alice'), never the body's self-asserted 'bob'");

        var hasBob = provenanceJson.Contains("\"approver\":\"bob\"")
            || provenanceJson.Contains("\"approver\": \"bob\"");
        hasBob.Should().BeFalse(because: "the body's 'approver' field must be ignored entirely");
    }

    // ─── Human-assisted agent identity must match caller ─────────────────────

    [Fact]
    public async Task PostApply_WithHumanAssistedAgentIdentityMismatchingCaller_ReturnsBadRequest()
    {
        using var client = _factory.CreateAuthenticatedClient("alice");

        var body = JsonSerializer.Serialize(new
        {
            envelope = new
            {
                id = Guid.NewGuid(),
                createdAt = DateTimeOffset.UtcNow,
                agent = new { kind = "human-assisted", identity = "bob" }, // mismatch
                targetWorkflowId = "planning-application",
                rationale = "Slice 3c: agent-identity cross-stamp regression"
            }
        }, WorkflowProjector.CanonicalOptions);

        var response = await client.PostAsync(
            "/api/workflow-authoring/workflows/planning/apply",
            new StringContent(body, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(
            HttpStatusCode.BadRequest,
            because: "a human-assisted envelope must name the calling principal to prevent authorship laundering");
    }
}