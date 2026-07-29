using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Wayfinder.Umbraco.Models;
using UmbracoPrism.MockBusinessApp.Services;
using Wayfinder.Models.ServiceDesign;
using Wayfinder.Models.ServiceDesign.Components;
using Wayfinder.Services.Sanitization;

namespace UmbracoPrism.Core.Tests.ServiceDesign.ProcessManager;

/// <summary>
/// Regression coverage for a real bug: <see cref="BusinessAppProcessManager"/> overrides
/// <c>Advance</c> to run MockBusinessApp-specific registered actions around a plain stage
/// transition, and correctly delegates to <c>base.Advance</c> for gateway-bound transitions — but
/// its "change:" branch used to be its own copy-pasted (and, over time, stale) reimplementation
/// instead of delegating the same way. When the base class's "change:" handling was fixed to also
/// move the right cursor, this override's independent copy silently kept the old, cursor-blind
/// behavior, making every "Change" link on a summary-list a no-op in the real app even though unit
/// tests against the base <c>ProcessManagerEngine</c> passed. This test suite exists specifically
/// so a future change to the base class's "change:" behavior can't drift out of sync with this
/// override again without a test catching it — see also
/// UmbracoPrism.Core.Tests.Workflow.Components.WorkflowChangeLinkCursorTests for the base-class
/// coverage of the underlying cursor-vs-CurrentState mechanics.
/// </summary>
public class BusinessAppWorkflowEngineChangeLinkTests : IDisposable
{
    private const string Tenant = "PRISM-DEMO";
    private const string User = "demo@prism.local";

    private readonly string _testSeedDir;
    private readonly BusinessAppProcessManager _engine;

    public BusinessAppWorkflowEngineChangeLinkTests()
    {
        _testSeedDir = Path.Combine(Directory.GetCurrentDirectory(), $"test-seeds-{Guid.NewGuid()}");
        Directory.CreateDirectory(Path.Combine(_testSeedDir, "service-blueprints"));
        SeedWorkflow();

        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.ContentRootPath).Returns(_testSeedDir);

        var logger = new Mock<ILogger<BusinessAppProcessManager>>();
        var sanitizer = new Mock<IServiceContentSanitizer>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string?>())).Returns<string?>(h => h ?? string.Empty);
        _engine = new BusinessAppProcessManager(logger.Object, mockEnv.Object, sanitizer.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testSeedDir))
        {
            Directory.Delete(_testSeedDir, recursive: true);
        }
    }

    private void SeedWorkflow()
    {
        // Mirrors garden-waste-permit's real shape: every state route targets a gateway (required
        // by ValidateGatewayRouting), so cursors are populated by the time the review stage is
        // reached — the precondition that made the stale override a real, always-triggered bug.
        var definition = new ServiceBlueprint
        {
            DefinitionKey = "change-link-business-app-test",
            DisplayName = "Change Link Business App Test",
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
                                }
                            ]
                        }
                    ]
                }
            ],
            Transitions =
            [
                new RouteFile { FromState = "how-many-bins", ToState = "fee-gateway", Action = "continue" },
                new RouteFile { FromState = "fee-gateway", ToState = "collection-fee", Action = "" }
            ],
            Metadata = new ServiceBlueprintMetadata
            {
                AuthoredServiceBlueprintId = new Guid("aaaabbbb-cccc-dddd-eeee-000000000095"),
                Gateways =
                [
                    new ServiceBlueprintGatewayDefinition
                    {
                        Key = "fee-gateway",
                        DisplayName = "Fee gateway",
                        GatewayType = "Join",
                        QueueKey = "web-user",
                        RequiredIncomingQueues = ["web-user"]
                    }
                ]
            }
        };

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(
            Path.Combine(_testSeedDir, "service-blueprints", "change-link-business-app-test.json"),
            JsonSerializer.Serialize(definition, jsonOptions));
    }

    [Fact]
    public void ChangeLink_ThroughBusinessAppWorkflowEngine_NavigatesBackToTargetState()
    {
        _engine.ResetAll();

        var initial = _engine.GetCurrent("change-link-business-app-test", Tenant, User);
        var afterBins = _engine.Advance(
            initial.InstanceId, Tenant, User, "continue", initial.StateVersion,
            new Dictionary<string, object?> { ["binCount"] = "5" });
        afterBins.Render!.StateDisplayName.Should().Be("Collection Fee");

        var instanceBeforeChange = _engine.GetAllInstances().Single(i => i.InstanceId == afterBins.InstanceId);
        instanceBeforeChange.Cursors.Should().NotBeEmpty(
            "the gateway hop populates cursors, which is what made the stale override's copy a real bug");

        var afterChange = _engine.Advance(
            afterBins.InstanceId, Tenant, User, "change:how-many-bins", afterBins.StateVersion, null);

        afterChange.Render!.StateDisplayName.Should().Be("How many bins do you have?",
            "BusinessAppProcessManager's override must delegate change: handling to the (fixed) base " +
            "implementation, not run its own stale copy");
    }
}
