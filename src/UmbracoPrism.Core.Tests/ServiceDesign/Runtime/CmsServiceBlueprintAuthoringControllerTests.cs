using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using UmbracoPrism.Core.Controllers;
using UmbracoPrism.Core.Services.ServiceDesign;
using UmbracoPrism.Shared.Models.ServiceDesign;
using UmbracoPrism.ProcessManager.Abstractions;
using UmbracoPrism.ProcessManager.Services;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Runtime;

/// <summary>
/// Verifies the backoffice CMS Workflow authoring controller's routing/wiring — it's a thin
/// pass-through to <see cref="ServiceBlueprintAuthoringService"/>, so these tests focus on the
/// IActionResult mapping (404/409/400/200) and the single-queue contract, not authoring logic
/// itself (already covered by ServiceBlueprintAuthoringService's own tests).
/// </summary>
public class CmsServiceBlueprintAuthoringControllerTests
{
    private static CmsServiceBlueprintAuthoringController BuildController(Mock<IServiceBlueprintSourceStore> store) =>
        new(new ServiceBlueprintAuthoringService(store.Object));

    private static ServiceBlueprint BuildWorkflow(int version = 0) => new()
    {
        DefinitionKey = "apply-for-a-juggling-licence",
        DisplayName = "Apply for a Juggling Licence",
        Version = version,
        InitialStage = "eligibility",
        Stages = [
            new StageDefinition { StageKey = "eligibility", DisplayName = "Eligibility", StageType = "Question" }
        ]
    };

    [Fact]
    public void GetQueues_ReturnsExactlyTheSingleWellKnownQueue()
    {
        var controller = BuildController(new Mock<IServiceBlueprintSourceStore>());

        var result = controller.GetQueues().Should().BeOfType<OkObjectResult>().Subject;
        var queues = ((IEnumerable<object>)result.Value!).ToList();

        queues.Should().ContainSingle();
    }

    [Fact]
    public async Task ReadWorkflow_UnknownKey_ReturnsNotFound()
    {
        var store = new Mock<IServiceBlueprintSourceStore>();
        store.Setup(s => s.LoadAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceBlueprint?)null);

        var result = await BuildController(store).ReadServiceBlueprint("missing", CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ReadWorkflow_KnownKey_ReturnsOkWithTheWorkflow()
    {
        var store = new Mock<IServiceBlueprintSourceStore>();
        store.Setup(s => s.LoadAsync("apply-for-a-juggling-licence", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildWorkflow(version: 3));

        var result = await BuildController(store).ReadServiceBlueprint("apply-for-a-juggling-licence", CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<ServiceBlueprint>()
            .Which.Version.Should().Be(3);
    }

    [Fact]
    public async Task SaveWorkflow_RouteKeyMismatchesBodyKey_ReturnsBadRequestWithoutCallingTheStore()
    {
        var store = new Mock<IServiceBlueprintSourceStore>();

        var result = await BuildController(store).SaveServiceBlueprint(
            "some-other-key", BuildWorkflow(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        store.Verify(s => s.SaveAsync(It.IsAny<ServiceBlueprint>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveWorkflow_VersionConflict_ReturnsConflict()
    {
        var store = new Mock<IServiceBlueprintSourceStore>();
        store.Setup(s => s.SaveAsync(It.IsAny<ServiceBlueprint>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceBlueprintSaveResult(Saved: false, CurrentVersion: 5, Location: "prismCmsServiceBlueprint"));

        var result = await BuildController(store).SaveServiceBlueprint(
            "apply-for-a-juggling-licence", BuildWorkflow(version: 2), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task SaveWorkflow_Success_ReturnsOk()
    {
        var store = new Mock<IServiceBlueprintSourceStore>();
        store.Setup(s => s.SaveAsync(It.IsAny<ServiceBlueprint>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceBlueprintSaveResult(Saved: true, CurrentVersion: 1, Location: "prismCmsServiceBlueprint"));

        var result = await BuildController(store).SaveServiceBlueprint(
            "apply-for-a-juggling-licence", BuildWorkflow(version: 0), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }
}
