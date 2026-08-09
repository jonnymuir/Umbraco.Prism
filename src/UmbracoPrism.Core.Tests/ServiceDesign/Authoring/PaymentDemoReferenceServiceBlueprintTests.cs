extern alias MockBusinessApp;

using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Wayfinder.Models.ServiceDesign;
using Wayfinder.Models.ServiceDesign.Components;
using Wayfinder.Services.Sanitization;
using Wayfinder.Engine.Abstractions;
using Wayfinder.Engine.Services;
using MockBusinessAppWorkflowEngine = MockBusinessApp::UmbracoPrism.MockBusinessApp.Services.BusinessAppProcessManager;
using MockReferenceWorkflowDefinitionStore = MockBusinessApp::UmbracoPrism.MockBusinessApp.Services.ReferenceServiceBlueprintStore;
using MockReferenceWorkflowQueues = MockBusinessApp::UmbracoPrism.MockBusinessApp.Services.ReferenceQueues;
using MockReferenceWorkflowRepository = MockBusinessApp::UmbracoPrism.MockBusinessApp.Services.ReferenceServiceBlueprintRepository;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Authoring;

public class PaymentDemoReferenceWorkflowTests
{
    [Fact]
    public void PaymentDemo_UsesQueueOnlyDefinition_WithStateAndGatewayRoutes()
    {
        var definition = MockReferenceWorkflowRepository.GetReferenceWorkflows()
            .Single(workflow => workflow.Key == "payment-demo")
            .Value;

        definition.Queues.Should().ContainSingle(queue =>
            queue.Key == MockReferenceWorkflowQueues.WebUser && queue.DisplayName == "Applicant touchpoints");
        definition.Queues.Should().ContainSingle(queue =>
            queue.Key == MockReferenceWorkflowQueues.BusinessUser && queue.DisplayName == "Payments team touchpoints");

        definition.Stages.Single(state => state.StageKey == "enter-details").QueueKey
            .Should().Be(MockReferenceWorkflowQueues.WebUser);
        definition.Stages.Single(state => state.StageKey == "confirm-payment-received").QueueKey
            .Should().Be(MockReferenceWorkflowQueues.BusinessUser);
        definition.Stages.Single(state => state.StageKey == "enter-details").Routes.Should().ContainSingle(route =>
            route.Target == "submit-payment" && route.Trigger == "submit");
        definition.Stages.Single(state => state.StageKey == "confirm-payment-received").Routes.Should().ContainSingle(route =>
            route.Target == "await-payment-confirmation" && route.Trigger == "confirm");
        definition.Gateways!.Should().NotContain(gateway => gateway.Key == "confirm-payment-route");

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
            && item.StageKey == "confirm-payment-received"
            && item.AvailableActions.Any(action => action.ActionKey == "confirm"));

        var workItem = queueWork.Items.Single(item => item.InstanceId == current.InstanceId);
        var afterConfirmation = engine.Advance(
            current.InstanceId,
            "tenant-1",
            "applicant@example.com",
            MockReferenceWorkflowQueues.BusinessUserProfile(),
            "confirm",
            workItem.StateVersion,
            new Dictionary<string, object?>
            {
                ["confirmationReference"] = "REF-100",
                ["amountReceived"] = 42.50m
            });

        // "payment-complete" belongs to the web-user queue — the confirming business user has
        // no visibility into it (Wayfinder dropped its implicit cross-queue fallback rendering:
        // a queue with no visible work item now always gets ACCESS_DENIED), so the confirming
        // actor's own envelope is correctly ACCESS_DENIED. Confirm completion from the applicant
        // who actually owns that queue instead.
        afterConfirmation.ResponseState.Should().Be("error");
        var applicantView = engine.GetCurrent(
            "payment-demo",
            "tenant-1",
            "applicant@example.com",
            MockReferenceWorkflowQueues.WebUserProfile());
        applicantView.ResponseState.Should().Be("complete");
        applicantView.Render!.StateDisplayName.Should().Be("Payment complete");
        applicantView.Render.Components.Should().Contain(component =>
            component.Type == "panel" && component.Heading == "Payment confirmed");
    }

    [Fact]
    public void PaymentDemo_UsesDirectConfirmationRoute_WithoutExtraConfirmationGateway()
    {
        var definition = MockReferenceWorkflowRepository.GetReferenceWorkflows()
            .Single(workflow => workflow.Key == "payment-demo")
            .Value;

        definition.Gateways.Should().NotContain(gateway => gateway.Key == "confirm-payment-route");
        definition.Gateways.Should().HaveCount(2);

        definition.Stages.Single(state => state.StageKey == "confirm-payment-received")
            .Routes.Should().ContainSingle(route =>
                route.Target == "await-payment-confirmation"
                && route.Trigger == "confirm"
                && route.RequiresRole == "reviewer");
    }

    [Fact]
    public void QueueAccessProfile_UsesConfiguredQueues_NotHostSpecificRoleNames()
    {
        var definition = new ServiceBlueprint
        {
            DefinitionKey = "queue-test",
            DisplayName = "Queue Test",
            Version = 1,
            InitialStage = "citizen-start",
            RequestPolicy = "single",
            Queues = new[]
            {
                new QueueDefinition
                {
                    Key = "citizen-queue",
                    DisplayName = "Citizen queue"
                },
                new QueueDefinition
                {
                    Key = "finance-queue",
                    DisplayName = "Finance queue"
                }
            },
            Stages = new[]
            {
                new StageDefinition
                {
                    StageKey = "citizen-start",
                    DisplayName = "Citizen start",
                    QueueKey = "citizen-queue",
                    Components = new Component[] { new FieldsetComponent() },
                    Routes = new[]
                    {
                        new ServiceBlueprintRouteDefinition
                        {
                            Id = "citizen-start--submit--finance-review",
                            Target = "finance-review",
                            Trigger = "submit"
                        }
                    }
                },
                new StageDefinition
                {
                    StageKey = "finance-review",
                    DisplayName = "Finance review",
                    QueueKey = "finance-queue",
                    Components = new Component[] { new FieldsetComponent() },
                    Routes = new[]
                    {
                        new ServiceBlueprintRouteDefinition
                        {
                            Id = "finance-review--approve--done",
                            Target = "done",
                            Trigger = "approve",
                            RequiresRole = "reviewer"
                        }
                    }
                },
                new StageDefinition
                {
                    StageKey = "done",
                    DisplayName = "Done",
                    QueueKey = "citizen-queue",
                    Components = new Component[] { new PanelComponent { Heading = "Done" } }
                }
            }
            // No Transitions block needed: both stages above already declare their own Routes,
            // which GetOutgoingTransitions checks before ever falling back to Transitions — this
            // array was fully redundant even before Transitions was removed from Wayfinder.
        };

        var sanitizer = new Mock<IServiceContentSanitizer>();
        sanitizer.Setup(service => service.Sanitize(It.IsAny<string?>())).Returns<string?>(value => value ?? string.Empty);

        var engine = new Wayfinder.Engine.Services.ProcessManagerEngine(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Wayfinder.Engine.Services.ProcessManagerEngine>.Instance,
            new InMemoryDefinitionStore(definition),
            sanitizer.Object);

        var citizenProfile = new ActorProfile
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

        var financeProfile = new ActorProfile
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

        // financeProfile can only see "finance-queue" — "done" belongs to "citizen-queue", so
        // the acting profile's own envelope from Advance is correctly ACCESS_DENIED (Wayfinder
        // dropped its implicit cross-queue fallback rendering: a queue with no visible work item
        // now always gets ACCESS_DENIED, never a peek at whatever stage the instance landed on).
        // Confirm completion from the queue that actually owns "done" instead.
        complete.ResponseState.Should().Be("error");
        var citizenView = engine.GetCurrent("queue-test", "tenant-1", "user-1", citizenProfile);
        citizenView.ResponseState.Should().Be("complete");
    }

    [Fact]
    public void CapabilitiesProvider_WebUser_ReferencesTheComponentCatalogDirectly()
    {
        var capabilities = MockReferenceWorkflowQueues.CapabilitiesProvider();

        capabilities.GetSupportedComponentTypes(MockReferenceWorkflowQueues.WebUser)
            .Should().BeEquivalentTo(ComponentTypeRegistry.AllDiscriminators,
                because: "web-user's declared capability must be Prism's own catalog contract, not a stale hand-copied list");
    }

    [Theory]
    [InlineData("payment-demo")]
    [InlineData("money-modeller")]
    public void CapabilitiesProvider_RealSeed_ValidatesCleanlyAgainstBusinessUserCapability(string definitionKey)
    {
        var definition = MockReferenceWorkflowRepository.GetReferenceWorkflows()
            .Single(workflow => workflow.Key == definitionKey)
            .Value;

        var service = new ServiceBlueprintAuthoringService(new NoOpWorkflowSourceStore(), queueCapabilities: MockReferenceWorkflowQueues.CapabilitiesProvider());

        var outcome = service.Validate(definition);

        outcome.Diagnostics.Should().NotContain(d => d.Code == "QUEUE_CAPABILITY_UNSUPPORTED_COMPONENT",
            because: "MockBusinessApp's cut-down admin capability set was chosen specifically to cover this real seed's business-user components");
    }

    private static MockBusinessAppWorkflowEngine CreateEngine()
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(env => env.ContentRootPath).Returns(AppContext.BaseDirectory);

        var sanitizer = new Mock<IServiceContentSanitizer>();
        sanitizer.Setup(service => service.Sanitize(It.IsAny<string?>())).Returns<string?>(value => value ?? string.Empty);

        return new MockBusinessAppWorkflowEngine(
            new Mock<ILogger<MockBusinessAppWorkflowEngine>>().Object,
            environment.Object,
            sanitizer.Object,
            new MockReferenceWorkflowDefinitionStore());
    }

    private static string[] InputFieldKeys(ServiceBlueprint workflow, string stageKey) =>
        workflow.Stages
            .Single(stage => stage.StageKey == stageKey)
            .Components
            .OfType<FieldsetComponent>()
            .SelectMany(fieldset => fieldset.Children.OfType<InputComponent>())
            .Select(component => component.FieldKey)
            .ToArray();

    private sealed class NoOpWorkflowSourceStore : IServiceBlueprintSourceStore
    {
        public Task<IReadOnlyList<ServiceBlueprintSourceSummary>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ServiceBlueprintSourceSummary>>([]);

        public Task<ServiceBlueprint?> LoadAsync(string definitionKey, CancellationToken ct = default) =>
            Task.FromResult<ServiceBlueprint?>(null);

        public Task<ServiceBlueprintSaveResult> SaveAsync(ServiceBlueprint workflow, int expectedVersion, CancellationToken ct = default) =>
            throw new NotSupportedException("Not needed for Validate-only tests.");

        public Task<bool> DeleteAsync(string definitionKey, CancellationToken ct = default) =>
            throw new NotSupportedException("Not needed for Validate-only tests.");
    }

    private sealed class InMemoryDefinitionStore : IServiceBlueprintStore
    {
        private readonly ServiceBlueprint _definition;

        public InMemoryDefinitionStore(ServiceBlueprint definition)
        {
            _definition = definition;
        }

        public IReadOnlyDictionary<string, ServiceBlueprint> LoadDefinitions(ILogger logger) =>
            new Dictionary<string, ServiceBlueprint>
            {
                [_definition.DefinitionKey] = _definition
            };
    }
}
