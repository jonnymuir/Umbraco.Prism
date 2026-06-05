extern alias MockBusinessApp;

using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.Shared.Models.Workflow.Components;
using UmbracoPrism.Shared.Services.Sanitization;
using UmbracoPrism.WorkflowRuntime.Abstractions;
using MockBusinessAppWorkflowEngine = MockBusinessApp::UmbracoPrism.MockBusinessApp.Services.BusinessAppWorkflowEngine;
using MockReferenceWorkflowDefinitionStore = MockBusinessApp::UmbracoPrism.MockBusinessApp.Services.ReferenceWorkflowDefinitionStore;
using MockReferenceWorkflowQueues = MockBusinessApp::UmbracoPrism.MockBusinessApp.Services.ReferenceWorkflowQueues;
using MockReferenceWorkflowRepository = MockBusinessApp::UmbracoPrism.MockBusinessApp.Services.ReferenceWorkflowRepository;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

public class PaymentDemoReferenceWorkflowTests
{
    [Fact]
    public void PaymentDemo_UsesQueueOnlyDefinition_WithStateAndGatewayRoutes()
    {
        var definition = MockReferenceWorkflowRepository.GetReferenceWorkflows()
            .Single(workflow => workflow.Key == "payment-demo")
            .Value;

        definition.Queues.Should().ContainSingle(queue =>
            queue.Key == MockReferenceWorkflowQueues.WebUser && queue.DisplayName == "Applicant");
        definition.Queues.Should().ContainSingle(queue =>
            queue.Key == MockReferenceWorkflowQueues.BusinessUser && queue.DisplayName == "Payments team");

        definition.States.Single(state => state.StateKey == "enter-details").QueueKey
            .Should().Be(MockReferenceWorkflowQueues.WebUser);
        definition.States.Single(state => state.StateKey == "confirm-payment-received").QueueKey
            .Should().Be(MockReferenceWorkflowQueues.BusinessUser);
        definition.States.Single(state => state.StateKey == "enter-details").Routes.Should().ContainSingle(route =>
            route.Target == "submit-payment" && route.Trigger == "submit");

        var joinGateway = definition.Gateways!.Single(gateway => gateway.Key == "await-payment-confirmation");
        joinGateway.QueueKey.Should().Be(MockReferenceWorkflowQueues.WebUser);
        joinGateway.RequiredIncomingQueues.Should().Equal(
            MockReferenceWorkflowQueues.WebUser,
            MockReferenceWorkflowQueues.BusinessUser);
        joinGateway.Routes.Should().ContainSingle(route =>
            route.Target == "payment-complete" && route.Trigger == "release");

        InputFieldKeys(definition, "enter-details").Should().Contain(new[]
        {
            "cardholderName",
            "paymentReference",
            "receiptEmail",
            "amount"
        });
        InputFieldKeys(definition, "confirm-payment-received").Should().Contain(new[]
        {
            "confirmationReference",
            "amountReceived",
            "notes"
        });
    }

    [Fact]
    public void PaymentDemo_WaitsAtJoinUntilBackOfficeConfirmationArrives_ThenCompletes()
    {
        var engine = CreateEngine();

        var current = engine.GetCurrent(
            "payment-demo",
            "tenant-1",
            "applicant@example.com",
            MockReferenceWorkflowQueues.WebUserProfile());
        current.ResponseState.Should().Be("render");
        current.Render!.StateDisplayName.Should().Be("Enter payment details");

        var afterSubmit = engine.Advance(
            current.InstanceId,
            "tenant-1",
            "applicant@example.com",
            MockReferenceWorkflowQueues.WebUserProfile(),
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

        var waitingInstance = engine.GetAllInstances().Single(instance => instance.InstanceId == current.InstanceId);
        waitingInstance.Cursors.Should().Contain(cursor =>
            cursor.CurrentNodeKey == "await-payment-confirmation"
            && cursor.IsAtGateway
            && cursor.QueueKey == MockReferenceWorkflowQueues.WebUser);
        waitingInstance.Cursors.Should().Contain(cursor =>
            cursor.CurrentNodeKey == "confirm-payment-received"
            && !cursor.IsAtGateway
            && cursor.QueueKey == MockReferenceWorkflowQueues.BusinessUser);
        waitingInstance.JoinArrivals["await-payment-confirmation"].Should().HaveCount(1);

        var queueWork = engine.GetQueueWorkItems(MockReferenceWorkflowQueues.BusinessUserProfile());
        queueWork.Items.Should().ContainSingle(item =>
            item.InstanceId == current.InstanceId
            && item.QueueName == MockReferenceWorkflowQueues.BusinessUser
            && item.StateKey == "confirm-payment-received"
            && item.AvailableActions.Any(action => action.ActionKey == "confirm"));

        var workItem = queueWork.Items.Single(item => item.InstanceId == current.InstanceId);
        var afterConfirmation = engine.Advance(
            current.InstanceId,
            "tenant-1",
            "applicant@example.com",
            MockReferenceWorkflowQueues.BusinessUserProfile(),
            "confirm",
            workItem.StateVersion,
            fieldValues: null);

        afterConfirmation.ResponseState.Should().Be("complete");
        afterConfirmation.Render!.StateDisplayName.Should().Be("Payment complete");
        afterConfirmation.Render.Components.Should().Contain(component =>
            component.Type == "panel" && component.Heading == "Payment confirmed");
    }

    [Fact]
    public void QueueAccessProfile_UsesConfiguredQueues_NotHostSpecificRoleNames()
    {
        var definition = new WorkflowDefinitionFile
        {
            DefinitionKey = "queue-test",
            DisplayName = "Queue Test",
            Version = 1,
            InitialState = "citizen-start",
            InstancePolicy = "single",
            Queues = new[]
            {
                new WorkflowQueueDefinition
                {
                    Key = "citizen-queue",
                    DisplayName = "Citizen queue"
                },
                new WorkflowQueueDefinition
                {
                    Key = "finance-queue",
                    DisplayName = "Finance queue"
                }
            },
            States = new[]
            {
                new StepDefinition
                {
                    StateKey = "citizen-start",
                    DisplayName = "Citizen start",
                    QueueKey = "citizen-queue",
                    Components = new PrismComponent[] { new FieldsetComponent() },
                    Routes = new[]
                    {
                        new WorkflowRouteDefinition
                        {
                            Id = "citizen-start--submit--finance-review",
                            Target = "finance-review",
                            Trigger = "submit"
                        }
                    }
                },
                new StepDefinition
                {
                    StateKey = "finance-review",
                    DisplayName = "Finance review",
                    QueueKey = "finance-queue",
                    Components = new PrismComponent[] { new FieldsetComponent() },
                    Routes = new[]
                    {
                        new WorkflowRouteDefinition
                        {
                            Id = "finance-review--approve--done",
                            Target = "done",
                            Trigger = "approve",
                            RequiresRole = "reviewer"
                        }
                    }
                },
                new StepDefinition
                {
                    StateKey = "done",
                    DisplayName = "Done",
                    QueueKey = "citizen-queue",
                    Components = new PrismComponent[] { new PanelComponent { Heading = "Done" } }
                }
            },
            Transitions = new[]
            {
                new WorkflowTransitionFile
                {
                    FromState = "citizen-start",
                    ToState = "finance-review",
                    Action = "submit"
                },
                new WorkflowTransitionFile
                {
                    FromState = "finance-review",
                    ToState = "done",
                    Action = "approve",
                    RequiresRole = "reviewer"
                }
            }
        };

        var sanitizer = new Mock<IWorkflowContentSanitizer>();
        sanitizer.Setup(service => service.Sanitize(It.IsAny<string?>())).Returns<string?>(value => value ?? string.Empty);

        var engine = new UmbracoPrism.WorkflowRuntime.Services.WorkflowRuntimeEngine(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<UmbracoPrism.WorkflowRuntime.Services.WorkflowRuntimeEngine>.Instance,
            new InMemoryDefinitionStore(definition),
            sanitizer.Object);

        var citizenProfile = new WorkflowAccessProfile
        {
            VisibleQueues = new[] { "citizen-queue" },
            StartableQueues = new[] { "citizen-queue" },
            ActionableQueues = new[] { "citizen-queue" }
        };

        var start = engine.GetCurrent("queue-test", "tenant-1", "user-1", citizenProfile);
        var afterSubmit = engine.Advance(
            start.InstanceId,
            "tenant-1",
            "user-1",
            citizenProfile,
            "submit",
            start.StateVersion,
            fieldValues: null);

        var financeProfile = new WorkflowAccessProfile
        {
            VisibleQueues = new[] { "finance-queue" },
            ActionableQueues = new[] { "finance-queue" },
            RestrictToInstanceOwner = false
        };

        var financeItems = engine.GetQueueWorkItems(financeProfile);
        financeItems.Items.Should().ContainSingle(item =>
            item.QueueName == "finance-queue"
            && item.AvailableActions.Any(action => action.ActionKey == "approve"));

        var financeWork = financeItems.Items.Single();
        var complete = engine.Advance(
            financeWork.InstanceId,
            "tenant-1",
            "user-1",
            financeProfile,
            "approve",
            financeWork.StateVersion,
            fieldValues: null);

        complete.ResponseState.Should().Be("complete");
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
            new MockReferenceWorkflowDefinitionStore());
    }

    private static string[] InputFieldKeys(WorkflowDefinitionFile workflow, string stageKey) =>
        workflow.States
            .Single(stage => stage.StateKey == stageKey)
            .Components
            .OfType<FieldsetComponent>()
            .SelectMany(fieldset => fieldset.Children.OfType<InputComponent>())
            .Select(component => component.FieldKey)
            .ToArray();

    private sealed class InMemoryDefinitionStore : IWorkflowDefinitionStore
    {
        private readonly WorkflowDefinitionFile _definition;

        public InMemoryDefinitionStore(WorkflowDefinitionFile definition)
        {
            _definition = definition;
        }

        public IReadOnlyDictionary<string, WorkflowDefinitionFile> LoadDefinitions(ILogger logger) =>
            new Dictionary<string, WorkflowDefinitionFile>
            {
                [_definition.DefinitionKey] = _definition
            };
    }
}
