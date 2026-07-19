using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UmbracoPrism.Shared.Builders;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.Shared.Services.Sanitization;
using UmbracoPrism.WorkflowRuntime.Abstractions;
using UmbracoPrism.WorkflowRuntime.Models;
using UmbracoPrism.WorkflowRuntime.Services;

namespace UmbracoPrism.Core.Tests.Workflow.Runtime;

/// <summary>
/// Verifies <see cref="WorkflowRuntimeEngine"/> actually routes instance state through an
/// injected <see cref="IWorkflowInstanceStore"/> rather than its own private state — the seam
/// a host (e.g. Prism CMS Workflow) relies on to swap in durable/session-scoped storage.
/// </summary>
public class WorkflowRuntimeEngineInstanceStoreTests
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
        stored.WorkflowKey.Should().Be("test-workflow");
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
    public void NoInstanceStoreSupplied_DefaultsToProcessLocalInMemoryBehaviour()
    {
        // Backward-compatibility guarantee: existing hosts that never pass an instanceStore
        // (MockBusinessApp, TestSite's business-workflow demo) must see unchanged behaviour.
        var engine = CreateEngine(instanceStore: null);

        var envelope = engine.GetCurrent("test-workflow", Tenant, User, action: "start-new");

        envelope.ResponseState.Should().NotBe("error");
        engine.GetAllInstances().Should().ContainSingle(i => i.InstanceId == envelope.InstanceId);
    }

    private static WorkflowRuntimeEngine CreateEngine(IWorkflowInstanceStore? instanceStore)
    {
        var sanitizer = new Mock<IWorkflowContentSanitizer>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(x => x);

        return new TestableWorkflowRuntimeEngine(
            NullLogger<TestableWorkflowRuntimeEngine>.Instance,
            sanitizer.Object,
            BuildDefinition(),
            instanceStore);
    }

    private static WorkflowDefinitionFile BuildDefinition() =>
        new WorkflowDefinitionBuilder()
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

    private static WorkflowInstanceState CreateInstance(string instanceId, string currentState) => new()
    {
        InstanceId = instanceId,
        WorkflowKey = "test-workflow",
        TenantId = Tenant,
        UserId = User,
        CurrentState = currentState,
        StateVersion = 1,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private sealed class TestableWorkflowRuntimeEngine : WorkflowRuntimeEngine
    {
        public TestableWorkflowRuntimeEngine(
            ILogger<TestableWorkflowRuntimeEngine> logger,
            IWorkflowContentSanitizer sanitizer,
            WorkflowDefinitionFile definition,
            IWorkflowInstanceStore? instanceStore)
            : base(logger, new SingleDefinitionStore(definition), sanitizer, instanceStore: instanceStore)
        {
        }
    }

    private sealed class SingleDefinitionStore(WorkflowDefinitionFile definition) : IWorkflowDefinitionStore
    {
        public IReadOnlyDictionary<string, WorkflowDefinitionFile> LoadDefinitions(ILogger logger) =>
            new Dictionary<string, WorkflowDefinitionFile>(StringComparer.OrdinalIgnoreCase)
            {
                [definition.DefinitionKey] = definition
            };
    }

    /// <summary>A pass-through <see cref="IWorkflowInstanceStore"/> that just wraps a dictionary
    /// — used to prove the engine round-trips through whatever store it's given.</summary>
    private sealed class RecordingWorkflowInstanceStore : IWorkflowInstanceStore
    {
        private readonly Dictionary<string, WorkflowInstanceState> _instances = new();

        public bool TryGet(string instanceId, out WorkflowInstanceState instance) =>
            _instances.TryGetValue(instanceId, out instance!);

        public void Save(WorkflowInstanceState instance) => _instances[instance.InstanceId] = instance;

        public bool Remove(string instanceId) => _instances.Remove(instanceId);

        public void Clear() => _instances.Clear();

        public IEnumerable<WorkflowInstanceState> GetAll() => _instances.Values;
    }
}
