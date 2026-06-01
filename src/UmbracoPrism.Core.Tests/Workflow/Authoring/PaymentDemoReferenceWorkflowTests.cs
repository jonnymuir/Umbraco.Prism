extern alias MockBusinessApp;

using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using UmbracoPrism.Shared.Models.Workflow.Components;
using UmbracoPrism.Shared.Services.Sanitization;
using UmbracoPrism.WorkflowEditor.Authoring;
using MockBusinessAppWorkflowEngine = MockBusinessApp::UmbracoPrism.MockBusinessApp.Services.BusinessAppWorkflowEngine;
using MockReferenceWorkflowDefinitionStore = MockBusinessApp::UmbracoPrism.MockBusinessApp.Services.ReferenceWorkflowDefinitionStore;
using MockReferenceWorkflowRepository = MockBusinessApp::UmbracoPrism.MockBusinessApp.Services.ReferenceWorkflowRepository;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

public class PaymentDemoReferenceWorkflowTests
{
    [Fact]
    public void PaymentDemo_ProjectsToJoinWaitTopology_WithUpdatedStageComponents()
    {
        var authored = MockReferenceWorkflowRepository.GetReferenceWorkflows()
            .Single(workflow => workflow.Key == "payment-demo")
            .Value;

        var projection = new WorkflowProjector().Project(authored);

        projection.HasErrors.Should().BeFalse();
        projection.File.Metadata!.Gateways.Should().ContainSingle(g =>
            g.Key == "submit-payment" && g.GatewayType == "Split");
        var joinGateway = projection.File.Metadata!.Gateways!.Single(g => g.Key == "await-payment-confirmation");
        joinGateway.GatewayType.Should().Be("Join");
        joinGateway.RequiredIncomingLanes.Should().Equal("applicant", "payments");

        projection.File.Transitions.Should().Contain(t =>
            t.FromState == "enter-details" && t.ToState == "submit-payment" && t.Action == "submit");
        projection.File.Transitions.Should().Contain(t =>
            t.FromState == "submit-payment" && t.ToState == "await-payment-confirmation");
        projection.File.Transitions.Should().Contain(t =>
            t.FromState == "submit-payment" && t.ToState == "confirm-payment-received");
        projection.File.Transitions.Should().Contain(t =>
            t.FromState == "confirm-payment-received"
            && t.ToState == "await-payment-confirmation"
            && t.Action == "confirm"
            && t.RequiresRole == "reviewer");
        projection.File.Transitions.Should().Contain(t =>
            t.FromState == "await-payment-confirmation" && t.ToState == "payment-complete" && t.Action == "release");

        AuthoredInputFieldKeys(authored, "enter-details").Should().Contain([
            "cardholderName",
            "paymentReference",
            "receiptEmail",
            "amount"
        ]);
        AuthoredInputFieldKeys(authored, "confirm-payment-received").Should().Contain([
            "confirmationReference",
            "amountReceived",
            "notes"
        ]);
        authored.Stages.Single(stage => stage.StageKey == "payment-complete")
            .Components
            .OfType<PanelComponent>()
            .Should()
            .ContainSingle(panel => panel.Heading == "Payment confirmed");
    }

    [Fact]
    public void PaymentDemo_WaitsAtJoinUntilBackOfficeConfirmationArrives_ThenCompletes()
    {
        var engine = CreateEngine();

        var current = engine.GetCurrent("payment-demo", "tenant-1", "applicant@example.com");
        current.ResponseState.Should().Be("render");
        current.Render!.StateDisplayName.Should().Be("Enter payment details");

        var afterSubmit = engine.Advance(
            current.InstanceId,
            "tenant-1",
            "applicant@example.com",
            "submit",
            current.StateVersion,
            new Dictionary<string, object?>
            {
                ["cardholderName"] = "Blathers Example",
                ["paymentReference"] = "PAY-82",
                ["receiptEmail"] = "applicant@example.com",
                ["amount"] = 42.50m
            });

        afterSubmit.ResponseState.Should().Be("defer");
        afterSubmit.Render!.StateDisplayName.Should().Be("Awaiting payment confirmation");
        afterSubmit.Render.Components.Should().ContainSingle(component => component.Type == "waiting");
        afterSubmit.Render.Components.Single(component => component.Type == "waiting").Content.Should()
            .Contain("payments team to confirm receipt of your payment");

        var waitingInstance = engine.GetAllInstances().Single(instance => instance.InstanceId == current.InstanceId);
        waitingInstance.Cursors.Should().Contain(cursor =>
            cursor.CurrentNodeKey == "await-payment-confirmation"
            && cursor.IsAtGateway
            && cursor.LaneKey == "applicant");
        waitingInstance.Cursors.Should().Contain(cursor =>
            cursor.CurrentNodeKey == "confirm-payment-received"
            && !cursor.IsAtGateway
            && cursor.LaneKey == "payments");
        waitingInstance.JoinArrivals["await-payment-confirmation"].Should().HaveCount(1,
            because: "the applicant path should already be parked at the join while the payments lane is still outstanding");

        var afterConfirmation = engine.AdvanceAsReviewer(current.InstanceId, "confirm");

        afterConfirmation.ResponseState.Should().Be("complete");
        afterConfirmation.Render!.StateDisplayName.Should().Be("Payment complete");
        afterConfirmation.Render.Components.Should().Contain(component =>
            component.Type == "panel" && component.Heading == "Payment confirmed");

        var completedInstance = engine.GetAllInstances().Single(instance => instance.InstanceId == current.InstanceId);
        completedInstance.JoinArrivals.Should().NotContainKey("await-payment-confirmation");
        completedInstance.Cursors.Should().ContainSingle(cursor =>
            cursor.CurrentNodeKey == "payment-complete" && !cursor.IsAtGateway);
    }

    private static MockBusinessAppWorkflowEngine CreateEngine()
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(env => env.ContentRootPath).Returns(AppContext.BaseDirectory);

        var sanitizer = new Mock<IWorkflowContentSanitizer>();
        sanitizer.Setup(service => service.Sanitize(It.IsAny<string?>())).Returns<string?>(value => value ?? string.Empty);

        return new MockBusinessAppWorkflowEngine(
            new Mock<ILogger<MockBusinessAppWorkflowEngine>>().Object,
            environment.Object,
            sanitizer.Object,
            new MockReferenceWorkflowDefinitionStore(new WorkflowProjector()));
    }

    private static string[] AuthoredInputFieldKeys(AuthoredWorkflow workflow, string stageKey) =>
        workflow.Stages
            .Single(stage => stage.StageKey == stageKey)
            .Components
            .OfType<FieldsetComponent>()
            .SelectMany(fieldset => fieldset.Children.OfType<InputComponent>())
            .Select(component => component.FieldKey)
            .ToArray();
}
