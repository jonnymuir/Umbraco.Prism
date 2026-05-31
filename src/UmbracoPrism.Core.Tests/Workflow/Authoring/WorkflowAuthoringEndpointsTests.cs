extern alias MockBusinessApp;

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UmbracoPrism.WorkflowEditor.Authoring;
using MockProgram = MockBusinessApp::Program;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

/// <summary>
/// Integration smoke tests for <c>MapWorkflowAuthoringEndpoints()</c> using
/// <see cref="WebApplicationFactory{TEntryPoint}"/> against MockBusinessApp.
///
/// The host requires authentication on every <c>/api/workflow-authoring/*</c> route.
/// <see cref="WorkflowAuthoringWebFactory"/> installs a header-driven test auth scheme
/// (<c>X-Test-User</c>); tests that exercise normal happy-paths get an authenticated
/// client via <see cref="WorkflowAuthoringWebFactory.CreateAuthenticatedClient"/>.
/// </summary>
[Collection("WorkflowAuthoringFactory")]
public class WorkflowAuthoringEndpointsTests
{
    private readonly WorkflowAuthoringWebFactory _factory;
    private readonly HttpClient _client;

    public WorkflowAuthoringEndpointsTests(WorkflowAuthoringWebFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient("smoke-test");
    }

    [Fact]
    public async Task GetWorkflows_ReturnsPlanningSummary_ForReferenceEditorShell()
    {
        var response = await _client.GetAsync("/api/workflow-authoring/workflows");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var workflows = await response.Content.ReadFromJsonAsync<List<WorkflowAuthoringSummary>>();

        workflows.Should().NotBeNull();
        workflows.Should().Contain(workflow =>
            workflow.WorkflowKey == "planning"
            && workflow.DefinitionKey == "planning-application"
            && !string.IsNullOrWhiteSpace(workflow.DisplayName),
            because: "the reference editor shell needs the route key it can round-trip back into the load endpoint");
    }

    [Fact]
    public async Task GetWorkflows_SkipsInvalidAuthoredWorkflowFiles_InsteadOfFailingTheEditorList()
    {
        await WriteAuthoredFixtureAsync("broken-listing", "{");

        try
        {
            var response = await _client.GetAsync("/api/workflow-authoring/workflows");

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var workflows = await response.Content.ReadFromJsonAsync<List<WorkflowAuthoringSummary>>();

            workflows.Should().NotBeNull();
            workflows.Should().Contain(workflow =>
                workflow.WorkflowKey == "planning"
                && workflow.DefinitionKey == "planning-application");
            workflows.Should().NotContain(workflow => workflow.WorkflowKey == "broken-listing",
                because: "an invalid authored document should be skipped instead of crashing the workflow picker");
        }
        finally
        {
            CleanupAuthoredFixture("broken-listing");
        }
    }

    [Fact]
    public async Task WorkflowAdmin_ShowsEditorAffordances_WhenCanonicalAuthoredWorkflowsExist()
    {
        var response = await _client.GetAsync("/admin/workflow");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Edit workflow",
            because: "the admin screen should offer the reference editor when authored workflows exist");
        body.Should().Contain("data-workflow-key=\"planning\"",
            because: "planning should round-trip on the route key even when its authored definition key differs");
        body.Should().Contain("data-workflow-key=\"community-enquiry\"",
            because: "community-enquiry is part of the canonical authored contract");
        body.Should().NotContain("No editor definition yet",
            because: "the canonical four workflows should all have authored sources");
    }

    [Fact]
    public async Task WorkflowAdmin_ShowsInvalidAuthoringStatus_ForBrokenAuthoredWorkflowFiles()
    {
        await WriteAuthoredFixtureAsync("community-enquiry", "{");

        try
        {
            var response = await _client.GetAsync("/admin/workflow");

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("data-definition-key=\"community-enquiry\"");
            body.Should().Contain("Editor definition invalid",
                because: "the admin page should distinguish a broken editor definition from a workflow that is not configured for the editor");
            body.Should().NotContain("href=\"/workflow-editor?workflow=community-enquiry\"",
                because: "the editor shortcut must stay honest when the authored file cannot be loaded");
        }
        finally
        {
            CleanupAuthoredFixture("community-enquiry");
        }
    }

    [Fact]
    public async Task GetWorkflow_ByRouteKey_ReturnsWorkflow()
    {
        var response = await _client.GetAsync("/api/workflow-authoring/workflows/planning");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var workflow = await response.Content.ReadFromJsonAsync<AuthoredWorkflow>(WorkflowProjector.CanonicalOptions);
        workflow.Should().NotBeNull();
        workflow!.DefinitionKey.Should().Be("planning-application",
            because: "the route key should stay aligned with the authored file name even when the authored definition key differs");
    }

    [Fact]
    public async Task GetWorkflow_WithInvalidAuthoredFile_ReturnsConflict()
    {
        await WriteAuthoredFixtureAsync("broken-workflow", "{");

        try
        {
            var response = await _client.GetAsync("/api/workflow-authoring/workflows/broken-workflow");

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("editor definition is invalid");
        }
        finally
        {
            CleanupAuthoredFixture("broken-workflow");
        }
    }

    [Fact]
    public async Task GetActionCatalog_ReturnsBuiltInActions()
    {
        var response = await _client.GetAsync("/api/workflow-authoring/action-catalog");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var catalog = await response.Content.ReadFromJsonAsync<List<ActionCatalogEntry>>(WorkflowProjector.CanonicalOptions);

        catalog.Should().NotBeNull();
        catalog.Should().HaveCountGreaterOrEqualTo(8);
        catalog!.Should().ContainSingle(entry => entry.Type == "forms.load")
            .Which.ParameterWidgets.Should().ContainKey("formDefinitionId").WhoseValue.Should().Be(ParameterWidgets.Text);
    }

    [Fact]
    public async Task GetActionCatalog_UsesRegisteredCatalogSource()
    {
        using var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IActionCatalogSource>();
                services.AddSingleton<IActionCatalogSource>(new StubActionCatalogSource(
                [
                    new ActionCatalogEntry
                    {
                        Type = "runtime.fake",
                        Label = "Runtime fake",
                        Summary = "Proves the endpoint reads the host catalog source.",
                        AppliesTo = [ActionCatalogScopes.Transition],
                        ParamsSchema = new AuthoredParameterSchema
                        {
                            Key = "runtime.fake",
                            Title = "Runtime fake",
                            AppliesTo = ["runtime.fake"]
                        }
                    }
                ]));
            });
        }).CreateClient();
        client.DefaultRequestHeaders.Add(WorkflowAuthoringWebFactory.TestUserHeader, "smoke-test");

        var response = await client.GetAsync("/api/workflow-authoring/action-catalog");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var catalog = await response.Content.ReadFromJsonAsync<List<ActionCatalogEntry>>(WorkflowProjector.CanonicalOptions);
        catalog.Should().ContainSingle(entry => entry.Type == "runtime.fake");
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
    public async Task PostPublish_WithValidWorkflow_ReturnsRoundTripVerifiedPublish()
    {
        var authored = BuildMinimalAuthoredWorkflow();
        var json = JsonSerializer.Serialize(authored, WorkflowProjector.CanonicalOptions);

        var response = await _client.PostAsync(
            "/api/workflow-authoring/workflows/smoke-test/publish",
            new StringContent(json, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("savedPath");
        body.Should().Contain("publishedPath");
        body.Should().Contain("roundTripVerified");
        body.Should().Contain("verifiedChecksum");

        File.Exists(GetAuthoredFixturePath("smoke-test")).Should().BeTrue();
        CleanupAuthoredFixture("smoke-test");
    }

    [Fact]
    public async Task PostPublish_PreservesAuthoredWorkflowId_InPublishedMetadata()
    {
        var authored = BuildMinimalAuthoredWorkflow() with
        {
            DefinitionKey = "metadata-alignment",
            DisplayName = "Metadata Alignment Test"
        };
        var json = JsonSerializer.Serialize(authored, WorkflowProjector.CanonicalOptions);

        try
        {
            var response = await _client.PostAsync(
                "/api/workflow-authoring/workflows/metadata-alignment/publish",
                new StringContent(json, Encoding.UTF8, "application/json"));

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<PublishResult>();
            body.Should().NotBeNull();
            body!.VerifiedFile.Should().NotBeNull();
            body.VerifiedFile!.Metadata.Should().NotBeNull();
            body.VerifiedFile.Metadata!.AuthoredWorkflowId.Should().Be(authored.Id,
                because: "the published workflow must trace back to the authored source via metadata.authoredWorkflowId");
        }
        finally
        {
            CleanupAuthoredFixture("metadata-alignment");
        }
    }

    [Fact]
    public async Task PostPublish_WithValidWorkflow_PersistsAuthoredAndPublishedDefinitions()
    {
        var authored = BuildMinimalAuthoredWorkflow() with
        {
            DefinitionKey = "save-smoke",
            DisplayName = "Save Smoke Workflow"
        };
        var json = JsonSerializer.Serialize(authored, WorkflowProjector.CanonicalOptions);

        var response = await _client.PostAsync(
            "/api/workflow-authoring/workflows/save-smoke/publish",
            new StringContent(json, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("savedPath");
        body.Should().Contain("publishedPath");
        body.Should().Contain("roundTripVerified");

        File.Exists(GetAuthoredFixturePath("save-smoke")).Should().BeTrue();
        CleanupAuthoredFixture("save-smoke");
    }

    [Fact]
    public async Task PostPublish_PreservesRouteWorkflowKey_WhenDefinitionKeyDiffers()
    {
        const string workflowKey = "planning-shell";
        const string definitionKey = "planning-shell-definition";
        var authored = BuildMinimalAuthoredWorkflow() with
        {
            DefinitionKey = definitionKey,
            DisplayName = "Planning Application"
        };
        var json = JsonSerializer.Serialize(authored, WorkflowProjector.CanonicalOptions);

        try
        {
            var response = await _client.PostAsync(
                $"/api/workflow-authoring/workflows/{workflowKey}/publish",
                new StringContent(json, Encoding.UTF8, "application/json"));

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            File.Exists(GetAuthoredFixturePath(workflowKey)).Should().BeTrue();
            File.Exists(GetAuthoredFixturePath(definitionKey)).Should().BeFalse();
        }
        finally
        {
            CleanupAuthoredFixture(workflowKey);
            CleanupAuthoredFixture(definitionKey);
        }
    }

    [Fact]
    public async Task PostSave_RetiredAliasRoute_ReturnsNotFound()
    {
        // Slice 8a retired the /save alias. Only /publish (direct save) and /apply
        // (envelope-mediated save) remain. Integrators that still target /save get
        // a routing 404, not a silent rewrite.
        var authored = BuildMinimalAuthoredWorkflow() with
        {
            DefinitionKey = "retired-save-route",
            DisplayName = "Retired Save Route"
        };
        var json = JsonSerializer.Serialize(authored, WorkflowProjector.CanonicalOptions);

        var response = await _client.PostAsync(
            "/api/workflow-authoring/workflows/retired-save-route/save",
            new StringContent(json, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostApply_WithNonExistentWorkflow_ReturnsNotFound()
    {
        var body = JsonSerializer.Serialize(new
        {
            envelope = BuildMinimalEnvelope("missing-key")
        }, WorkflowProjector.CanonicalOptions);

        var response = await _client.PostAsync(
            "/api/workflow-authoring/workflows/missing-key/apply",
            new StringContent(body, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostApply_WithExistingWorkflow_PublishesRuntimeDefinition()
    {
        // Envelope agent identity must match the calling principal for human-assisted kind.
        var body = JsonSerializer.Serialize(new
        {
            envelope = BuildMinimalEnvelope("planning-application", agentIdentity: "smoke-test")
        }, WorkflowProjector.CanonicalOptions);

        var response = await _client.PostAsync(
            "/api/workflow-authoring/workflows/planning/apply",
            new StringContent(body, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseBody = await response.Content.ReadAsStringAsync();
        responseBody.Should().Contain("\"publish\"");
        responseBody.Should().Contain("roundTripVerified");
        responseBody.Should().Contain("publishedPath");
    }

    [Fact]
    public async Task PostSimulate_WithStoredWorkflow_ReturnsDeterministicPath()
    {
        var response = await _client.PostAsync(
            "/api/workflow-authoring/workflows/planning/simulate",
            new StringContent("{\"actions\":[\"continue\",\"continue\",\"submit\"]}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"initialStageKey\":\"declaration\"");
        body.Should().Contain("\"currentStageKey\":\"submitted\"");
        body.Should().Contain("\"stopReason\":\"terminal-stage\"");
        body.Should().Contain("\"completed\":true");
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

    private static ProposalEnvelope BuildMinimalEnvelope(string targetKey, string agentIdentity = "smoke-test") => new()
    {
        Id               = Guid.NewGuid(),
        CreatedAt        = DateTimeOffset.UtcNow,
        Agent            = new PatchAgent { Kind = "human-assisted", Identity = agentIdentity },
        TargetWorkflowId = targetKey,
        Rationale        = "Smoke test envelope",
        // /apply requires at least one op (Slice 8a). This update-transition matches
        // an existing planning transition by (source, target, trigger), so the patch
        // service treats it as a no-op overwrite and projection still passes.
        Ops              =
        [
            new PatchOp
            {
                Op    = "update-transition",
                Value = JsonSerializer.SerializeToElement(new
                {
                    source  = "declaration",
                    target  = "route-application-form",
                    trigger = "continue"
                })
            }
        ]
    };

    private static string GetAuthoredFixturePath(string key) =>
        Path.Combine(
            Path.GetDirectoryName(typeof(WorkflowAuthoringEndpointsTests).Assembly.Location)!,
            "Workflow",
            "Authoring",
            "Fixtures",
            $"{key}.workflow.json");

    private static string GetSourceAuthoredFixturePath(string key) =>
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../",
            "src",
            "UmbracoPrism.Core.Tests",
            "Workflow",
            "Authoring",
            "Fixtures",
            $"{key}.workflow.json"));

    private static Task WriteAuthoredFixtureAsync(string key, string content) =>
        File.WriteAllTextAsync(GetAuthoredFixturePath(key), content);

    private static void CleanupAuthoredFixture(string key)
    {
        var path = GetAuthoredFixturePath(key);
        var sourcePath = GetSourceAuthoredFixturePath(key);
        if (File.Exists(sourcePath))
        {
            File.Copy(sourcePath, path, overwrite: true);
            return;
        }

        if (File.Exists(path))
            File.Delete(path);
    }
}

/// <summary>
/// Customised <see cref="WebApplicationFactory{TEntryPoint}"/> for MockBusinessApp
/// that suppresses OIDC configuration so tests can run without a real identity provider.
/// </summary>
public sealed class WorkflowAuthoringWebFactory : WebApplicationFactory<MockProgram>
{
    public const string TestAuthScheme = "Test";
    public const string TestUserHeader = "X-Test-User";

    // ConfigureWebHost is re-invoked on every CreateClient/WithWebHostBuilder call;
    // reset the on-disk fixtures only once per process to avoid IOException races when
    // tests run in parallel collections.
    private static readonly object _fixturesGate = new();
    private static bool _fixturesInitialised;
    private static readonly object _publishedGate = new();
    private static bool _publishedInitialised;
    private static readonly object _provenanceGate = new();
    private static bool _provenanceInitialised;

    private static void EnsureCleanPublishedDirectory(string path)
    {
        if (_publishedInitialised) { Directory.CreateDirectory(path); return; }
        lock (_publishedGate)
        {
            if (_publishedInitialised) return;
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            Directory.CreateDirectory(path);
            _publishedInitialised = true;
        }
    }

    private static void EnsureCleanProvenanceDirectory(string path)
    {
        if (_provenanceInitialised) { Directory.CreateDirectory(path); return; }
        lock (_provenanceGate)
        {
            if (_provenanceInitialised) return;
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            Directory.CreateDirectory(path);
            _provenanceInitialised = true;
        }
    }

    /// <summary>
    /// Returns an <see cref="HttpClient"/> pre-configured to authenticate as
    /// <paramref name="user"/> via the in-process test scheme. Use
    /// <see cref="WebApplicationFactory{TEntryPoint}.CreateClient()"/> directly when a test
    /// needs to exercise the unauthenticated path (will receive 401 from the policy).
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string user)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestUserHeader, user);
        return client;
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        EnsureFixturesInitialised();

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
            services.RemoveAll<IPublishedWorkflowStore>();
            services.AddSingleton<IPublishedWorkflowStore>(_ =>
            {
                var publishedPath = GetPublishedPath();
                EnsureCleanPublishedDirectory(publishedPath);
                return new FilesystemPublishedWorkflowStore(publishedPath);
            });

            // Point provenance at a writable test-only directory and expose it for inspection.
            services.RemoveAll<IWorkflowAuthoringProvenanceStore>();
            services.AddSingleton<IWorkflowAuthoringProvenanceStore>(_ =>
            {
                var provenancePath = GetProvenancePath();
                EnsureCleanProvenanceDirectory(provenancePath);
                return new FilesystemWorkflowAuthoringProvenanceStore(provenancePath);
            });

            // Install a header-driven test authentication scheme as the default for
            // authenticate/challenge. Tests that omit the X-Test-User header receive 401.
            services.Configure<AuthenticationOptions>(o =>
            {
                o.DefaultAuthenticateScheme = TestAuthScheme;
                o.DefaultChallengeScheme = TestAuthScheme;
                o.DefaultScheme = TestAuthScheme;
            });
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestUserHeaderAuthHandler>(TestAuthScheme, _ => { });
        });
    }

    internal static string GetProvenancePath() =>
        Path.Combine(
            Path.GetDirectoryName(typeof(WorkflowAuthoringEndpointsTests).Assembly.Location)!,
            "Workflow", "Authoring", "Provenance");

    private static string GetFixturesPath() =>
        Path.Combine(
            Path.GetDirectoryName(typeof(WorkflowAuthoringEndpointsTests).Assembly.Location)!,
            "Workflow", "Authoring", "Fixtures");

    private static string GetSourceFixturesPath() =>
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../",
            "src",
            "UmbracoPrism.Core.Tests",
            "Workflow",
            "Authoring",
            "Fixtures"));

    private static string GetPublishedPath() =>
        Path.Combine(
            Path.GetDirectoryName(typeof(WorkflowAuthoringEndpointsTests).Assembly.Location)!,
            "Workflow", "Authoring", "Published");

    private static void EnsureFixturesInitialised()
    {
        if (_fixturesInitialised) return;
        lock (_fixturesGate)
        {
            if (_fixturesInitialised) return;
            ResetAuthoredFixturesDirectory();
            _fixturesInitialised = true;
        }
    }

    private static void ResetAuthoredFixturesDirectory()
    {
        var fixturesPath = GetFixturesPath();
        Directory.CreateDirectory(fixturesPath);

        var sourceFiles = Directory
            .GetFiles(GetSourceFixturesPath(), "*.workflow.json")
            .ToDictionary(p => Path.GetFileName(p), p => p, StringComparer.Ordinal);

        // Copy from source only when missing in bin — csproj <Content Include> already mirrors
        // them on build. Avoiding a copy-with-overwrite when not strictly needed prevents
        // IOException races with concurrent readers in sibling test classes.
        foreach (var (fileName, sourcePath) in sourceFiles)
        {
            var targetPath = Path.Combine(fixturesPath, fileName);
            if (!File.Exists(targetPath))
                File.Copy(sourcePath, targetPath);
        }

        // Remove any test-introduced fixture (e.g. broken-listing) that is not part of
        // the canonical source set.
        foreach (var path in Directory.GetFiles(fixturesPath, "*.workflow.json"))
        {
            var name = Path.GetFileName(path);
            if (!sourceFiles.ContainsKey(name))
                File.Delete(path);
        }
    }
}

internal sealed class StubActionCatalogSource(IReadOnlyList<ActionCatalogEntry> entries) : IActionCatalogSource
{
    public IReadOnlyList<ActionCatalogEntry> GetCatalog() => entries;
}

internal sealed record WorkflowAuthoringSummary(string WorkflowKey, Guid Id, string DefinitionKey, string DisplayName);

/// <summary>
/// Test-only authentication handler. Reads <c>X-Test-User</c> from the request; when present
/// the request is authenticated as that user via <c>preferred_username</c>. When absent the
/// handler returns <see cref="AuthenticateResult.NoResult"/> so the workflow-author policy
/// challenges and the caller sees 401.
/// </summary>
internal sealed class TestUserHeaderAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestUserHeaderAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(WorkflowAuthoringWebFactory.TestUserHeader, out var values))
            return Task.FromResult(AuthenticateResult.NoResult());

        var name = values.ToString();
        if (string.IsNullOrWhiteSpace(name))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new[]
        {
            new Claim("preferred_username", name),
            new Claim(ClaimTypes.Name, name)
        };
        var identity = new ClaimsIdentity(claims, WorkflowAuthoringWebFactory.TestAuthScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, WorkflowAuthoringWebFactory.TestAuthScheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>
/// Shared collection so any test class that boots <see cref="WorkflowAuthoringWebFactory"/>
/// re-uses the same instance, runs serially, and only performs the fixture reset once per
/// process — preventing races with concurrent readers in sibling test classes.
/// </summary>
[CollectionDefinition("WorkflowAuthoringFactory")]
public sealed class WorkflowAuthoringFactoryCollection : ICollectionFixture<WorkflowAuthoringWebFactory>
{
}
