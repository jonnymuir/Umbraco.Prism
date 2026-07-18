using FluentAssertions;
using UmbracoPrism.WorkflowRuntime.Models;
using UmbracoPrism.WorkflowRuntime.Stores;

namespace UmbracoPrism.Core.Tests.Workflow.Runtime;

/// <summary>
/// Behavioural contract tests for the default <see cref="IWorkflowInstanceStore"/> — the
/// same CRUD contract every implementation (in-memory or otherwise) must satisfy.
/// </summary>
public class InMemoryWorkflowInstanceStoreTests
{
    [Fact]
    public void TryGet_UnknownInstance_ReturnsFalse()
    {
        var store = new InMemoryWorkflowInstanceStore();

        store.TryGet("missing", out var instance).Should().BeFalse();
        instance.Should().BeNull();
    }

    [Fact]
    public void Save_ThenTryGet_ReturnsTheSameInstance()
    {
        var store = new InMemoryWorkflowInstanceStore();
        var instance = CreateInstance("instance-1");

        store.Save(instance);

        store.TryGet("instance-1", out var retrieved).Should().BeTrue();
        retrieved.Should().BeSameAs(instance);
    }

    [Fact]
    public void Save_ExistingInstanceId_Overwrites()
    {
        var store = new InMemoryWorkflowInstanceStore();
        store.Save(CreateInstance("instance-1", currentState: "first"));
        store.Save(CreateInstance("instance-1", currentState: "second"));

        store.TryGet("instance-1", out var retrieved).Should().BeTrue();
        retrieved.CurrentState.Should().Be("second");
    }

    [Fact]
    public void Remove_ExistingInstance_RemovesItAndReturnsTrue()
    {
        var store = new InMemoryWorkflowInstanceStore();
        store.Save(CreateInstance("instance-1"));

        store.Remove("instance-1").Should().BeTrue();
        store.TryGet("instance-1", out _).Should().BeFalse();
    }

    [Fact]
    public void Remove_UnknownInstance_ReturnsFalse()
    {
        var store = new InMemoryWorkflowInstanceStore();

        store.Remove("missing").Should().BeFalse();
    }

    [Fact]
    public void GetAll_ReturnsEveryStoredInstance()
    {
        var store = new InMemoryWorkflowInstanceStore();
        store.Save(CreateInstance("instance-1"));
        store.Save(CreateInstance("instance-2"));

        store.GetAll().Select(i => i.InstanceId).Should().BeEquivalentTo(["instance-1", "instance-2"]);
    }

    [Fact]
    public void Clear_RemovesEveryStoredInstance()
    {
        var store = new InMemoryWorkflowInstanceStore();
        store.Save(CreateInstance("instance-1"));
        store.Save(CreateInstance("instance-2"));

        store.Clear();

        store.GetAll().Should().BeEmpty();
    }

    private static WorkflowInstanceState CreateInstance(string instanceId, string currentState = "start") => new()
    {
        InstanceId = instanceId,
        WorkflowKey = "test-workflow",
        TenantId = "tenant-1",
        UserId = "user-1",
        CurrentState = currentState,
        StateVersion = 1,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };
}
