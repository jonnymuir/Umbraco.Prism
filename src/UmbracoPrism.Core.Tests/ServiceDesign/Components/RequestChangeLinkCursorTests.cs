using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayfinder.Models.ServiceDesign;
using Wayfinder.Models.ServiceDesign.Components;
using Wayfinder.Services.Sanitization;
using Wayfinder.Engine.Abstractions;
using Wayfinder.Engine.Services;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Components;

/// <summary>
/// A summary-list "Change" link submits a <c>change:{stateKey}</c> action, which
/// <see cref="ProcessManagerEngine.Advance"/> special-cases to jump the instance back to an
/// earlier stage. Reproduces a real bug found while producing a demo recording: every real Prism
/// workflow routes state-to-state through a gateway (required by
/// <see cref="ServiceBlueprint.ValidateGatewayRouting"/>), which means <c>instance.Cursors</c>
/// is populated by the time a user reaches a review/summary stage — but the render is built from
/// <c>Cursors</c>, not <c>CurrentStage</c>, once any cursor exists
/// (<c>ProcessManagerEngine.FindAccessibleWorkItems</c>). The original "change:" handler only
/// updated <c>CurrentStage</c>, so the jump was a silent no-op for any workflow with a gateway in
/// its path — which is effectively all of them — and the user landed right back where they
/// started. Confirmed live via a direct <c>simulate_workflow</c>/browser reproduction before fixing.
/// </summary>
public class RequestChangeLinkCursorTests
{
    private const string Tenant = "tenant-1";
    private const string User = "user-1";

    [Fact]
    public void ChangeLink_AfterGatewayRoutedStages_NavigatesBackToTargetState()
    {
        var (engine, _) = CreateEngine(BuildLinearGatewayRoutedDefinition());

        var initial = engine.GetCurrent("change-link-test", Tenant, User, action: "start-new");
        initial.Render!.StateDisplayName.Should().Be("How many bins do you have?");

        var afterBins = engine.Advance(
            initial.InstanceId, Tenant, User, "continue", initial.StateVersion,
            new Dictionary<string, object?> { ["binCount"] = "5" });
        afterBins.Render!.StateDisplayName.Should().Be("What's the property address?");

        var afterAddress = engine.Advance(
            afterBins.InstanceId, Tenant, User, "continue", afterBins.StateVersion,
            new Dictionary<string, object?> { ["propertyAddress"] = "14 Orchard Close" });
        afterAddress.Render!.StateDisplayName.Should().Be("Collection Fee");

        // Precondition that makes this bug real rather than theoretical: cursors are populated by
        // the time we reach the review stage, because every hop above went through a gateway.
        var instanceBeforeChange = engine.GetAllInstances().Single(i => i.InstanceId == afterAddress.InstanceId);
        instanceBeforeChange.Cursors.Should().NotBeEmpty(
            "gateway-routed stages populate cursors — this is what made the old CurrentStage-only fix a no-op");

        var afterChange = engine.Advance(
            afterAddress.InstanceId, Tenant, User, "change:how-many-bins", afterAddress.StateVersion, null);

        afterChange.Render!.StateDisplayName.Should().Be("How many bins do you have?",
            "the Change link must actually navigate back, not silently re-render the stale cursor position");

        // The value given earlier must still be there — a jump, not a reset.
        var binsField = afterChange.Render.Components
            .SelectMany(c => c.Fields)
            .First(f => f.FieldKey == "binCount");
        binsField.Value.Should().Be("5");
    }

    [Fact]
    public void ChangeLink_AfterNavigatingBack_ContinuingForwardReachesReviewStageAgain()
    {
        var (engine, _) = CreateEngine(BuildLinearGatewayRoutedDefinition());

        var initial = engine.GetCurrent("change-link-test", Tenant, User, action: "start-new");
        var afterBins = engine.Advance(
            initial.InstanceId, Tenant, User, "continue", initial.StateVersion,
            new Dictionary<string, object?> { ["binCount"] = "5" });
        var afterAddress = engine.Advance(
            afterBins.InstanceId, Tenant, User, "continue", afterBins.StateVersion,
            new Dictionary<string, object?> { ["propertyAddress"] = "14 Orchard Close" });

        var afterChange = engine.Advance(
            afterAddress.InstanceId, Tenant, User, "change:how-many-bins", afterAddress.StateVersion, null);

        // Real "change your mind" loop: update the value and continue forward through the same
        // gateway-routed path again, landing back on the review stage with the new value.
        var afterUpdatedBins = engine.Advance(
            afterChange.InstanceId, Tenant, User, "continue", afterChange.StateVersion,
            new Dictionary<string, object?> { ["binCount"] = "6" });
        afterUpdatedBins.Render!.StateDisplayName.Should().Be("What's the property address?");

        var afterFinal = engine.Advance(
            afterUpdatedBins.InstanceId, Tenant, User, "continue", afterUpdatedBins.StateVersion,
            new Dictionary<string, object?> { ["propertyAddress"] = "14 Orchard Close" });
        afterFinal.Render!.StateDisplayName.Should().Be("Collection Fee");

        var binsField = afterFinal.Render.Components
            .SelectMany(c => c.Fields)
            .First(f => f.FieldKey == "binCount");
        binsField.Value.Should().Be("6");
    }

    private static (ProcessManagerEngine engine, ServiceBlueprint definition) CreateEngine(
        ServiceBlueprint definition)
    {
        var sanitizer = new Mock<IServiceContentSanitizer>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(x => x);

        var engine = new TestableWorkflowRuntimeEngine(
            NullLogger<TestableWorkflowRuntimeEngine>.Instance, sanitizer.Object, definition);
        return (engine, definition);
    }

    // Mirrors garden-waste-permit's real shape: every state route targets a gateway (required by
    // ValidateGatewayRouting), even though this is a plain single-queue linear flow with no actual
    // fan-out — exactly the case that populated Cursors and exposed the bug.
    private static ServiceBlueprint BuildLinearGatewayRoutedDefinition() => new()
    {
        DefinitionKey = "change-link-test",
        DisplayName = "Change Link Test",
        Version = 1,
        InitialStage = "how-many-bins",
        RequestPolicy = "single",
        Queues = [new QueueDefinition { Key = "web-user", DisplayName = "Member", Actor = "member" }],
        Stages = [
            new StageDefinition
            {
                StageKey = "how-many-bins",
                DisplayName = "How many bins do you have?",
                QueueKey = "web-user",
                Components = [new NumberInputComponent { FieldKey = "binCount", Label = "Bins", Required = true }]
            },
            new StageDefinition
            {
                StageKey = "property-address",
                DisplayName = "What's the property address?",
                QueueKey = "web-user",
                Components = [new TextInputComponent { FieldKey = "propertyAddress", Label = "Address", Required = true }]
            },
            new StageDefinition
            {
                StageKey = "collection-fee",
                DisplayName = "Collection Fee",
                QueueKey = "web-user",
                Components =
                [
                    new SummaryListComponent
                    {
                        Children =
                        [
                            new NumberInputComponent
                            {
                                FieldKey = "binCount", Label = "Number of bins", ChangeStateKey = "how-many-bins"
                            },
                            new TextInputComponent
                            {
                                FieldKey = "propertyAddress", Label = "Property address", ChangeStateKey = "property-address"
                            }
                        ]
                    }
                ]
            }
        ],
        Transitions =
        [
            new RouteFile { FromState = "how-many-bins", ToState = "gateway-1", Action = "continue" },
            new RouteFile { FromState = "gateway-1", ToState = "property-address", Action = "" },
            new RouteFile { FromState = "property-address", ToState = "gateway-2", Action = "continue" },
            new RouteFile { FromState = "gateway-2", ToState = "collection-fee", Action = "" }
        ],
        Metadata = new ServiceBlueprintMetadata
        {
            AuthoredServiceBlueprintId = new Guid("aaaabbbb-cccc-dddd-eeee-000000000090"),
            Gateways =
            [
                new ServiceBlueprintGatewayDefinition
                {
                    Key = "gateway-1",
                    DisplayName = "Gateway 1",
                    GatewayType = "Join",
                    QueueKey = "web-user",
                    RequiredIncomingQueues = ["web-user"]
                },
                new ServiceBlueprintGatewayDefinition
                {
                    Key = "gateway-2",
                    DisplayName = "Gateway 2",
                    GatewayType = "Join",
                    QueueKey = "web-user",
                    RequiredIncomingQueues = ["web-user"]
                }
            ]
        }
    };

    /// <summary>
    /// Thin wrapper that registers the given definition without needing a filesystem store.
    /// </summary>
    private sealed class TestableWorkflowRuntimeEngine : ProcessManagerEngine
    {
        public TestableWorkflowRuntimeEngine(
            ILogger<TestableWorkflowRuntimeEngine> logger,
            IServiceContentSanitizer sanitizer,
            ServiceBlueprint definition)
            : base(logger, new SingleDefinitionStore(definition), sanitizer)
        {
        }
    }

    private sealed class SingleDefinitionStore : IServiceBlueprintStore
    {
        private readonly ServiceBlueprint _definition;

        public SingleDefinitionStore(ServiceBlueprint definition)
        {
            _definition = definition;
        }

        public IReadOnlyDictionary<string, ServiceBlueprint> LoadDefinitions(ILogger logger)
        {
            return new Dictionary<string, ServiceBlueprint>
            {
                [_definition.DefinitionKey] = _definition
            };
        }
    }
}
