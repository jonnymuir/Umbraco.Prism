using FluentAssertions;
using UmbracoPrism.ProcessManager.Models;
using UmbracoPrism.ProcessManager.Stores;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Runtime;

/// <summary>
/// Behavioural contract tests for the default <see cref="IServiceRequestStore"/> — the
/// same CRUD contract every implementation (in-memory or otherwise) must satisfy.
/// </summary>
public class InMemoryServiceRequestStoreTests
{
    [Fact]
    public void TryGet_UnknownInstance_ReturnsFalse()
    {
        var store = new InMemoryServiceRequestStore();

        store.TryGet("missing", out var instance).Should().BeFalse();
        instance.Should().BeNull();
    }

    [Fact]
    public void Save_ThenTryGet_ReturnsTheSameInstance()
    {
        var store = new InMemoryServiceRequestStore();
        var instance = CreateInstance("instance-1");

        store.Save(instance);

        store.TryGet("instance-1", out var retrieved).Should().BeTrue();
        retrieved.Should().BeSameAs(instance);
    }

    [Fact]
    public void Save_ExistingInstanceId_Overwrites()
    {
        var store = new InMemoryServiceRequestStore();
        store.Save(CreateInstance("instance-1", currentState: "first"));
        store.Save(CreateInstance("instance-1", currentState: "second"));

        store.TryGet("instance-1", out var retrieved).Should().BeTrue();
        retrieved.CurrentTouchpoint.Should().Be("second");
    }

    [Fact]
    public void Remove_ExistingInstance_RemovesItAndReturnsTrue()
    {
        var store = new InMemoryServiceRequestStore();
        store.Save(CreateInstance("instance-1"));

        store.Remove("instance-1").Should().BeTrue();
        store.TryGet("instance-1", out _).Should().BeFalse();
    }

    [Fact]
    public void Remove_UnknownInstance_ReturnsFalse()
    {
        var store = new InMemoryServiceRequestStore();

        store.Remove("missing").Should().BeFalse();
    }

    [Fact]
    public void GetAll_ReturnsEveryStoredInstance()
    {
        var store = new InMemoryServiceRequestStore();
        store.Save(CreateInstance("instance-1"));
        store.Save(CreateInstance("instance-2"));

        store.GetAll().Select(i => i.InstanceId).Should().BeEquivalentTo(["instance-1", "instance-2"]);
    }

    [Fact]
    public void Clear_RemovesEveryStoredInstance()
    {
        var store = new InMemoryServiceRequestStore();
        store.Save(CreateInstance("instance-1"));
        store.Save(CreateInstance("instance-2"));

        store.Clear();

        store.GetAll().Should().BeEmpty();
    }

    private static ServiceRequest CreateInstance(string instanceId, string currentState = "start") => new()
    {
        InstanceId = instanceId,
        BlueprintKey = "test-workflow",
        TenantId = "tenant-1",
        UserId = "user-1",
        CurrentTouchpoint = currentState,
        StateVersion = 1,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };
}
