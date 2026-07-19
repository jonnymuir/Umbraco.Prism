using FluentAssertions;
using Moq;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.Core.Services.Workflow;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.WorkflowRuntime.Abstractions;

namespace UmbracoPrism.Core.Tests.Workflow.Runtime;

/// <summary>
/// Verifies <see cref="UmbracoCmsWorkflowDefinitionStore"/>'s atomic compare-and-swap save
/// contract and its promise that a successful save reaches the live engine immediately.
/// </summary>
public class UmbracoCmsWorkflowDefinitionStoreTests
{
    private static (UmbracoCmsWorkflowDefinitionStore Store, Mock<IUmbracoDatabase> Db, Mock<IWorkflowRuntimeEngine> Engine) BuildStore()
    {
        var mockDb = new Mock<IUmbracoDatabase>();
        var dbFactory = new Mock<IUmbracoDatabaseFactory>();
        dbFactory.Setup(f => f.CreateDatabase()).Returns(mockDb.Object);

        var engine = new Mock<IWorkflowRuntimeEngine>();

        var store = new UmbracoCmsWorkflowDefinitionStore(dbFactory.Object, engine.Object);
        return (store, mockDb, engine);
    }

    private static WorkflowDefinitionFile BuildWorkflow(int version = 0) => new()
    {
        DefinitionKey = "apply-for-a-juggling-licence",
        DisplayName = "Apply for a Juggling Licence",
        Version = version,
        InitialState = "eligibility"
    };

    [Fact]
    public async Task SaveAsync_NewDefinition_InsertsAtVersionOneAndPushesToEngine()
    {
        var (store, db, engine) = BuildStore();
        db.Setup(d => d.FirstOrDefault<PrismCmsWorkflowDefinitionSchema>(
                It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((PrismCmsWorkflowDefinitionSchema?)null);

        var result = await store.SaveAsync(BuildWorkflow(), expectedVersion: 0);

        result.Saved.Should().BeTrue();
        result.CurrentVersion.Should().Be(1);
        db.Verify(d => d.Insert(It.Is<PrismCmsWorkflowDefinitionSchema>(r =>
            r.DefinitionKey == "apply-for-a-juggling-licence" && r.Version == 1)), Times.Once);
        engine.Verify(e => e.UpdateDefinition(
            "apply-for-a-juggling-licence", It.Is<WorkflowDefinitionFile>(w => w.Version == 1)), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_NewDefinition_WithNonZeroExpectedVersion_FailsWithoutInserting()
    {
        var (store, db, engine) = BuildStore();
        db.Setup(d => d.FirstOrDefault<PrismCmsWorkflowDefinitionSchema>(
                It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((PrismCmsWorkflowDefinitionSchema?)null);

        var result = await store.SaveAsync(BuildWorkflow(), expectedVersion: 3);

        result.Saved.Should().BeFalse();
        result.CurrentVersion.Should().Be(0);
        db.Verify(d => d.Insert(It.IsAny<PrismCmsWorkflowDefinitionSchema>()), Times.Never);
        engine.Verify(e => e.UpdateDefinition(It.IsAny<string>(), It.IsAny<WorkflowDefinitionFile>()), Times.Never);
    }

    [Fact]
    public async Task SaveAsync_MatchingExpectedVersion_UpdatesAtomicallyAndPushesToEngine()
    {
        var (store, db, engine) = BuildStore();
        db.Setup(d => d.FirstOrDefault<PrismCmsWorkflowDefinitionSchema>(
                It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns(new PrismCmsWorkflowDefinitionSchema { DefinitionKey = "apply-for-a-juggling-licence", Version = 2 });
        db.Setup(d => d.Execute(
                It.Is<string>(sql => sql.Contains("UPDATE prismCmsWorkflowDefinition")),
                It.IsAny<object[]>()))
            .Returns(1);

        var result = await store.SaveAsync(BuildWorkflow(), expectedVersion: 2);

        result.Saved.Should().BeTrue();
        result.CurrentVersion.Should().Be(3);
        engine.Verify(e => e.UpdateDefinition(
            "apply-for-a-juggling-licence", It.Is<WorkflowDefinitionFile>(w => w.Version == 3)), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_StaleExpectedVersion_FailsBeforeAttemptingUpdate()
    {
        var (store, db, engine) = BuildStore();
        db.Setup(d => d.FirstOrDefault<PrismCmsWorkflowDefinitionSchema>(
                It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns(new PrismCmsWorkflowDefinitionSchema { DefinitionKey = "apply-for-a-juggling-licence", Version = 5 });

        var result = await store.SaveAsync(BuildWorkflow(), expectedVersion: 2);

        result.Saved.Should().BeFalse();
        result.CurrentVersion.Should().Be(5);
        db.Verify(d => d.Execute(It.IsAny<string>(), It.IsAny<object[]>()), Times.Never);
        engine.Verify(e => e.UpdateDefinition(It.IsAny<string>(), It.IsAny<WorkflowDefinitionFile>()), Times.Never);
    }

    [Fact]
    public async Task SaveAsync_ConcurrentWriterWinsTheRace_ReturnsConflictWithoutPushingToEngine()
    {
        // Both writers read Version=2; a concurrent save already advanced it to 3, so this
        // writer's UPDATE ... WHERE Version = @expectedVersion matches zero rows.
        var (store, db, engine) = BuildStore();
        db.SetupSequence(d => d.FirstOrDefault<PrismCmsWorkflowDefinitionSchema>(
                It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns(new PrismCmsWorkflowDefinitionSchema { DefinitionKey = "apply-for-a-juggling-licence", Version = 2 })
            .Returns(new PrismCmsWorkflowDefinitionSchema { DefinitionKey = "apply-for-a-juggling-licence", Version = 3 });
        db.Setup(d => d.Execute(
                It.Is<string>(sql => sql.Contains("UPDATE prismCmsWorkflowDefinition")),
                It.IsAny<object[]>()))
            .Returns(0);

        var result = await store.SaveAsync(BuildWorkflow(), expectedVersion: 2);

        result.Saved.Should().BeFalse();
        result.CurrentVersion.Should().Be(3);
        engine.Verify(e => e.UpdateDefinition(It.IsAny<string>(), It.IsAny<WorkflowDefinitionFile>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_ExistingDefinition_DeletesRowAndRemovesFromEngine()
    {
        var (store, db, engine) = BuildStore();
        db.Setup(d => d.Execute(
                It.Is<string>(sql => sql.Contains("DELETE FROM prismCmsWorkflowDefinition")),
                It.IsAny<object[]>()))
            .Returns(1);

        var deleted = await store.DeleteAsync("apply-for-a-juggling-licence");

        deleted.Should().BeTrue();
        engine.Verify(e => e.RemoveDefinition("apply-for-a-juggling-licence"), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_UnknownDefinition_ReturnsFalseWithoutTouchingEngine()
    {
        var (store, db, engine) = BuildStore();
        db.Setup(d => d.Execute(
                It.Is<string>(sql => sql.Contains("DELETE FROM prismCmsWorkflowDefinition")),
                It.IsAny<object[]>()))
            .Returns(0);

        var deleted = await store.DeleteAsync("does-not-exist");

        deleted.Should().BeFalse();
        engine.Verify(e => e.RemoveDefinition(It.IsAny<string>()), Times.Never);
    }
}
