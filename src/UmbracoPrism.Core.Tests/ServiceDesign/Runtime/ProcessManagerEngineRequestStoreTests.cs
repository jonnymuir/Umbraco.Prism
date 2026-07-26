using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UmbracoPrism.Shared.Builders;
using UmbracoPrism.Shared.Models.ServiceDesign;
using UmbracoPrism.Shared.Services.Sanitization;
using UmbracoPrism.ProcessManager.Abstractions;
using UmbracoPrism.ProcessManager.Models;
using UmbracoPrism.ProcessManager.Services;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Runtime;

/// <summary>
/// Verifies <see cref="ProcessManagerEngine"/> actually routes instance state through an
/// injected <see cref="IServiceRequestStore"/> rather than its own private state — the seam
/// a host (e.g. Prism CMS Workflow) relies on to swap in durable/session-scoped storage.
/// </summary>
public class ProcessManagerEngineRequestStoreTests
{
    private const string Tenant = "tenant-1";
    private const string User = "user-1";

    [Fact]
    public void GetCurrent_StartNew_PersistsTheNewInstanceThroughTheInjectedStore()
    {
        var store = new RecordingWorkflowInstanceStore();
        var engine = CreateEngine(store);

        var envelope = engine.GetCurrent("test-workflow", Tenant, User, action: "start-new");

        envelope.ResponseState.Should().NotBe("error");
        store.TryGet(envelope.InstanceId, out var stored).Should().BeTrue(
            "the engine must save new instances through the injected store, not its own internal state");
        stored.BlueprintKey.Should().Be("test-workflow");
    }

    [Fact]
    public void GetCurrent_WithInstanceId_ResolvesTheInstanceFromTheInjectedStore()
    {
        var store = new RecordingWorkflowInstanceStore();
        var engine = CreateEngine(store);
        var seeded = CreateInstance("pre-seeded", "start");
        store.Save(seeded);

        var envelope = engine.GetCurrent("test-workflow", Tenant, User, instanceId: "pre-seeded");

        envelope.InstanceId.Should().Be("pre-seeded");
        envelope.ResponseState.Should().NotBe("error");
    }

    [Fact]
    public void GetCurrent_WithUnknownInstanceId_ReturnsInstanceNotFoundWithoutTouchingTheStore()
    {
        var store = new RecordingWorkflowInstanceStore();
        var engine = CreateEngine(store);

        var envelope = engine.GetCurrent("test-workflow", Tenant, User, instanceId: "does-not-exist");

        envelope.ResponseState.Should().Be("error");
        envelope.Problems.Should().ContainSingle(p => p.Code == "INSTANCE_NOT_FOUND");
    }

    [Fact]
    public void Reset_RemovesTheInstanceFromTheInjectedStore()
    {
        var store = new RecordingWorkflowInstanceStore();
        var engine = CreateEngine(store);
        store.Save(CreateInstance("instance-1", "start"));

        engine.Reset("instance-1").Should().BeTrue();
        store.TryGet("instance-1", out _).Should().BeFalse();
    }

    [Fact]
    public void ResetAll_ClearsEveryInstanceFromTheInjectedStore()
    {
        var store = new RecordingWorkflowInstanceStore();
        var engine = CreateEngine(store);
        store.Save(CreateInstance("instance-1", "start"));
        store.Save(CreateInstance("instance-2", "start"));

        engine.ResetAll();

        store.GetAll().Should().BeEmpty();
    }

    [Fact]
    public void GetCurrent_StartNew_DefaultsIsAuthenticatedToFalse()
    {
        // The base engine has no identity model of its own — ResolveIsAuthenticated is only
        // ever true when a host (e.g. CmsProcessManager) overrides it.
        var store = new RecordingWorkflowInstanceStore();
        var engine = CreateEngine(store);

        var envelope = engine.GetCurrent("test-workflow", Tenant, User, action: "start-new");

        store.TryGet(envelope.InstanceId, out var stored).Should().BeTrue();
        stored.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public void GetCurrent_StartNew_HostOverridingResolveIsAuthenticated_StampsTheNewInstance()
    {
        var store = new RecordingWorkflowInstanceStore();
        var sanitizer = new Mock<IServiceContentSanitizer>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(x => x);
        var engine = new AlwaysAuthenticatedWorkflowRuntimeEngine(
            NullLogger<AlwaysAuthenticatedWorkflowRuntimeEngine>.Instance,
            new SingleDefinitionStore(BuildDefinition()),
            sanitizer.Object,
            store);

        var envelope = engine.GetCurrent("test-workflow", Tenant, User, action: "start-new");

        store.TryGet(envelope.InstanceId, out var stored).Should().BeTrue();
        stored.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public void ClaimInstances_AnonymousInstanceWithNoConflict_IsRekeyedAndMarkedAuthenticated()
    {
        var store = new RecordingWorkflowInstanceStore();
        var engine = CreateEngine(store);
        store.Save(CreateInstance("anon-instance", "start") with { UserId = "anon-cookie-1" });

        var claimed = engine.ClaimInstances(Tenant, "anon-cookie-1", "member@example.test");

        claimed.Should().ContainSingle().Which.Should().Be("anon-instance");
        store.TryGet("anon-instance", out var stored).Should().BeTrue();
        stored.UserId.Should().Be("member@example.test");
        stored.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public void ClaimInstances_MemberAlreadyOwnsAnInstanceOfThatWorkflow_LeavesTheAnonymousOneUnclaimed()
    {
        var store = new RecordingWorkflowInstanceStore();
        var engine = CreateEngine(store);
        store.Save(CreateInstance("anon-instance", "start") with { UserId = "anon-cookie-1" });
        store.Save(CreateInstance("members-own-instance", "start") with { UserId = "member@example.test", IsAuthenticated = true });

        var claimed = engine.ClaimInstances(Tenant, "anon-cookie-1", "member@example.test");

        claimed.Should().BeEmpty();
        store.TryGet("anon-instance", out var stillAnonymous).Should().BeTrue();
        stillAnonymous.UserId.Should().Be("anon-cookie-1", "claiming must not overwrite the member's own existing instance");
    }

    [Fact]
    public void ClaimInstances_NoAnonymousInstances_ReturnsEmptyWithoutError()
    {
        var store = new RecordingWorkflowInstanceStore();
        var engine = CreateEngine(store);

        var claimed = engine.ClaimInstances(Tenant, "anon-cookie-with-nothing", "member@example.test");

        claimed.Should().BeEmpty();
    }

    [Fact]
    public void NoInstanceStoreSupplied_DefaultsToProcessLocalInMemoryBehaviour()
    {
        // Backward-compatibility guarantee: existing hosts that never pass an instanceStore
        // (MockBusinessApp, TestSite's business-workflow demo) must see unchanged behaviour.
        var engine = CreateEngine(instanceStore: null);

        var envelope = engine.GetCurrent("test-workflow", Tenant, User, action: "start-new");

        envelope.ResponseState.Should().NotBe("error");
        engine.GetAllInstances().Should().ContainSingle(i => i.InstanceId == envelope.InstanceId);
    }

    private static ProcessManagerEngine CreateEngine(IServiceRequestStore? instanceStore)
    {
        var sanitizer = new Mock<IServiceContentSanitizer>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(x => x);

        return new TestableWorkflowRuntimeEngine(
            NullLogger<TestableWorkflowRuntimeEngine>.Instance,
            sanitizer.Object,
            BuildDefinition(),
            instanceStore);
    }

    private static ServiceBlueprint BuildDefinition() =>
        new ServiceBlueprintBuilder()
            .Key("test-workflow")
            .DisplayName("Test Workflow")
            .StartsAt("start")
            .AddState("start", s => s
                .DisplayName("Start")
                .Panel("Start"))
            .AddState("done", s => s
                .DisplayName("Done")
                .Panel("Done"))
            .AddTransition("start", "done", "submit")
            .Build();

    private static ServiceRequest CreateInstance(string instanceId, string currentState) => new()
    {
        InstanceId = instanceId,
        BlueprintKey = "test-workflow",
        TenantId = Tenant,
        UserId = User,
        CurrentTouchpoint = currentState,
        StateVersion = 1,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private sealed class TestableWorkflowRuntimeEngine : ProcessManagerEngine
    {
        public TestableWorkflowRuntimeEngine(
            ILogger<TestableWorkflowRuntimeEngine> logger,
            IServiceContentSanitizer sanitizer,
            ServiceBlueprint definition,
            IServiceRequestStore? instanceStore)
            : base(logger, new SingleDefinitionStore(definition), sanitizer, instanceStore: instanceStore)
        {
        }
    }

    /// <summary>Proves a host can override ResolveIsAuthenticated to stamp new instances —
    /// mirrors how CmsProcessManager derives it from the live request's HttpContext.</summary>
    private sealed class AlwaysAuthenticatedWorkflowRuntimeEngine : ProcessManagerEngine
    {
        public AlwaysAuthenticatedWorkflowRuntimeEngine(
            ILogger<AlwaysAuthenticatedWorkflowRuntimeEngine> logger,
            IServiceBlueprintStore definitionStore,
            IServiceContentSanitizer sanitizer,
            IServiceRequestStore instanceStore)
            : base(logger, definitionStore, sanitizer, instanceStore: instanceStore)
        {
        }

        protected override bool ResolveIsAuthenticated(string tenantId, string userId) => true;
    }

    private sealed class SingleDefinitionStore(ServiceBlueprint definition) : IServiceBlueprintStore
    {
        public IReadOnlyDictionary<string, ServiceBlueprint> LoadDefinitions(ILogger logger) =>
            new Dictionary<string, ServiceBlueprint>(StringComparer.OrdinalIgnoreCase)
            {
                [definition.DefinitionKey] = definition
            };
    }

    /// <summary>A pass-through <see cref="IServiceRequestStore"/> that just wraps a dictionary
    /// — used to prove the engine round-trips through whatever store it's given.</summary>
    private sealed class RecordingWorkflowInstanceStore : IServiceRequestStore
    {
        private readonly Dictionary<string, ServiceRequest> _instances = new();

        public bool TryGet(string instanceId, out ServiceRequest instance) =>
            _instances.TryGetValue(instanceId, out instance!);

        public void Save(ServiceRequest instance) => _instances[instance.InstanceId] = instance;

        public bool Remove(string instanceId) => _instances.Remove(instanceId);

        public void Clear() => _instances.Clear();

        public IEnumerable<ServiceRequest> GetAll() => _instances.Values;
    }
}
