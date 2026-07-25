using System.Text.Json;
using FluentAssertions;
using Moq;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.Core.Services.Workflow;
using UmbracoPrism.WorkflowRuntime.Models;

namespace UmbracoPrism.Core.Tests.Workflow.Runtime;

/// <summary>
/// Verifies <see cref="UmbracoCmsWorkflowInstanceStore"/>'s expiry semantics — the mechanism
/// that lets a CMS Workflow instance survive an app-pool recycle (DB-backed) while still dying
/// with the visitor's session (sliding TTL, lazily enforced on read).
/// </summary>
public class UmbracoCmsWorkflowInstanceStoreTests
{
    private static (UmbracoCmsWorkflowInstanceStore Store, Mock<IUmbracoDatabase> Db) BuildStore()
    {
        var mockDb = new Mock<IUmbracoDatabase>();
        var dbFactory = new Mock<IUmbracoDatabaseFactory>();
        dbFactory.Setup(f => f.CreateDatabase()).Returns(mockDb.Object);

        var store = new UmbracoCmsWorkflowInstanceStore(dbFactory.Object, TimeSpan.FromMinutes(30));
        return (store, mockDb);
    }

    private static PrismCmsWorkflowInstanceSchema BuildRow(string instanceId, DateTime expiresUtc) => new()
    {
        InstanceId = instanceId,
        WorkflowKey = "apply-for-a-juggling-licence",
        TenantId = "tenant-1",
        UserId = "user-1",
        StateJson = JsonSerializer.Serialize(new WorkflowInstanceState
        {
            InstanceId = instanceId,
            WorkflowKey = "apply-for-a-juggling-licence",
            TenantId = "tenant-1",
            UserId = "user-1",
            CurrentState = "eligibility"
        }),
        ExpiresUtc = expiresUtc
    };

    [Fact]
    public void TryGet_UnexpiredRow_ReturnsTrueAndRefreshesTheSlidingWindow()
    {
        var (store, db) = BuildStore();
        db.Setup(d => d.FirstOrDefault<PrismCmsWorkflowInstanceSchema>(
                It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns(BuildRow("instance-1", DateTime.UtcNow.AddMinutes(10)));

        var found = store.TryGet("instance-1", out var instance);

        found.Should().BeTrue();
        instance.CurrentState.Should().Be("eligibility");
        db.Verify(d => d.Execute(
            It.Is<string>(sql => sql.Contains("UPDATE prismCmsWorkflowInstance SET ExpiresUtc")),
            It.IsAny<object[]>()), Times.Once);
    }

    [Fact]
    public void TryGet_ExpiredRow_ReturnsFalseAndDoesNotRefresh()
    {
        var (store, db) = BuildStore();
        db.Setup(d => d.FirstOrDefault<PrismCmsWorkflowInstanceSchema>(
                It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns(BuildRow("instance-1", DateTime.UtcNow.AddMinutes(-1)));

        var found = store.TryGet("instance-1", out var instance);

        found.Should().BeFalse();
        instance.Should().BeNull();
        db.Verify(d => d.Execute(It.IsAny<string>(), It.IsAny<object[]>()), Times.Never);
    }

    [Fact]
    public void TryGet_UnknownRow_ReturnsFalse()
    {
        var (store, db) = BuildStore();
        db.Setup(d => d.FirstOrDefault<PrismCmsWorkflowInstanceSchema>(
                It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((PrismCmsWorkflowInstanceSchema?)null);

        store.TryGet("missing", out _).Should().BeFalse();
    }

    [Fact]
    public void Save_NoExistingRow_Inserts()
    {
        var (store, db) = BuildStore();
        db.Setup(d => d.Execute(
                It.Is<string>(sql => sql.StartsWith("UPDATE prismCmsWorkflowInstance SET WorkflowKey")),
                It.IsAny<object[]>()))
            .Returns(0);

        store.Save(new WorkflowInstanceState
        {
            InstanceId = "instance-1",
            WorkflowKey = "apply-for-a-juggling-licence",
            TenantId = "tenant-1",
            UserId = "user-1",
            CurrentState = "eligibility"
        });

        db.Verify(d => d.Insert(It.Is<PrismCmsWorkflowInstanceSchema>(r =>
            r.InstanceId == "instance-1" && r.UserId == "user-1")), Times.Once);
    }

    [Fact]
    public void Save_ExistingRow_UpdatesInsteadOfInserting()
    {
        var (store, db) = BuildStore();
        db.Setup(d => d.Execute(
                It.Is<string>(sql => sql.StartsWith("UPDATE prismCmsWorkflowInstance SET WorkflowKey")),
                It.IsAny<object[]>()))
            .Returns(1);

        store.Save(new WorkflowInstanceState
        {
            InstanceId = "instance-1",
            WorkflowKey = "apply-for-a-juggling-licence",
            TenantId = "tenant-1",
            UserId = "user-1",
            CurrentState = "check-answers"
        });

        db.Verify(d => d.Insert(It.IsAny<PrismCmsWorkflowInstanceSchema>()), Times.Never);
    }

    [Fact]
    public void Save_AuthenticatedInstance_NewRow_InsertsWithNeverExpiresInsteadOfSlidingWindow()
    {
        var (store, db) = BuildStore();
        db.Setup(d => d.Execute(
                It.Is<string>(sql => sql.StartsWith("UPDATE prismCmsWorkflowInstance SET WorkflowKey")),
                It.IsAny<object[]>()))
            .Returns(0);

        store.Save(new WorkflowInstanceState
        {
            InstanceId = "instance-1",
            WorkflowKey = "apply-for-a-juggling-licence",
            TenantId = "tenant-1",
            UserId = "member@example.test",
            IsAuthenticated = true,
            CurrentState = "eligibility"
        });

        db.Verify(d => d.Insert(It.Is<PrismCmsWorkflowInstanceSchema>(r =>
            r.InstanceId == "instance-1" && r.ExpiresUtc == DateTime.MaxValue)), Times.Once);
    }

    [Fact]
    public void Save_AuthenticatedInstance_ExistingRow_UpdatesWithNeverExpires()
    {
        var (store, db) = BuildStore();
        DateTime? capturedExpiresUtc = null;
        db.Setup(d => d.Execute(
                It.Is<string>(sql => sql.StartsWith("UPDATE prismCmsWorkflowInstance SET WorkflowKey")),
                It.IsAny<object[]>()))
            .Callback<string, object[]>((_, args) => capturedExpiresUtc = (DateTime)args[4])
            .Returns(1);

        store.Save(new WorkflowInstanceState
        {
            InstanceId = "instance-1",
            WorkflowKey = "apply-for-a-juggling-licence",
            TenantId = "tenant-1",
            UserId = "member@example.test",
            IsAuthenticated = true,
            CurrentState = "check-answers"
        });

        capturedExpiresUtc.Should().Be(DateTime.MaxValue);
    }

    [Fact]
    public void Save_AnonymousInstance_StillUsesTheSlidingWindow()
    {
        var (store, db) = BuildStore();
        DateTime? capturedExpiresUtc = null;
        db.Setup(d => d.Execute(
                It.Is<string>(sql => sql.StartsWith("UPDATE prismCmsWorkflowInstance SET WorkflowKey")),
                It.IsAny<object[]>()))
            .Callback<string, object[]>((_, args) => capturedExpiresUtc = (DateTime)args[4])
            .Returns(1);

        store.Save(new WorkflowInstanceState
        {
            InstanceId = "instance-1",
            WorkflowKey = "apply-for-a-juggling-licence",
            TenantId = "tenant-1",
            UserId = "anon-cookie-1",
            IsAuthenticated = false,
            CurrentState = "check-answers"
        });

        capturedExpiresUtc.Should().NotBe(DateTime.MaxValue);
        capturedExpiresUtc.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(30), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void TryGet_AuthenticatedInstance_DoesNotRefreshTheAlreadyPermanentExpiry()
    {
        var (store, db) = BuildStore();
        var row = BuildRow("instance-1", DateTime.MaxValue);
        row.StateJson = JsonSerializer.Serialize(new WorkflowInstanceState
        {
            InstanceId = "instance-1",
            WorkflowKey = "apply-for-a-juggling-licence",
            TenantId = "tenant-1",
            UserId = "member@example.test",
            IsAuthenticated = true,
            CurrentState = "eligibility"
        });
        db.Setup(d => d.FirstOrDefault<PrismCmsWorkflowInstanceSchema>(
                It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns(row);

        var found = store.TryGet("instance-1", out var instance);

        found.Should().BeTrue();
        instance.IsAuthenticated.Should().BeTrue();
        db.Verify(d => d.Execute(
            It.Is<string>(sql => sql.Contains("UPDATE prismCmsWorkflowInstance SET ExpiresUtc")),
            It.IsAny<object[]>()), Times.Never,
            "an already-permanent row needs no sliding-window refresh — that would just be a wasted write");
    }

    [Fact]
    public void Remove_DeletesByInstanceId()
    {
        var (store, db) = BuildStore();
        db.Setup(d => d.Execute(
                It.Is<string>(sql => sql.Contains("DELETE FROM prismCmsWorkflowInstance WHERE InstanceId")),
                It.IsAny<object[]>()))
            .Returns(1);

        store.Remove("instance-1").Should().BeTrue();
    }

    [Fact]
    public void GetAll_OnlyFetchesUnexpiredRows()
    {
        var (store, db) = BuildStore();
        db.Setup(d => d.Fetch<PrismCmsWorkflowInstanceSchema>(
                It.Is<string>(sql => sql.Contains("WHERE ExpiresUtc >=")),
                It.IsAny<object[]>()))
            .Returns([BuildRow("instance-1", DateTime.UtcNow.AddMinutes(10))]);

        var all = store.GetAll().ToArray();

        all.Should().ContainSingle(i => i.InstanceId == "instance-1");
    }
}
