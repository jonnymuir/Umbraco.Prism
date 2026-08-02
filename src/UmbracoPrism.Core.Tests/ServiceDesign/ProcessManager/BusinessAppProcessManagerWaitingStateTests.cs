using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using UmbracoPrism.MockBusinessApp.Services;
using Wayfinder.Models.ServiceDesign;
using Wayfinder.Models.ServiceDesign.Components;
using Wayfinder.Services.Sanitization;

namespace UmbracoPrism.Core.Tests.ServiceDesign.ProcessManager;

// WaitingBuilderTests/WaitingBuilderFluentTests (fluent-builder mechanics for
// ServiceBlueprintBuilder.Waiting()/AddTransition) were removed along with
// Wayfinder.Builders.ServiceBlueprintBuilder itself (see Wayfinder's refactor! commit
// removing ServiceBlueprint.Transitions) — there is no builder left to test. WaitingComponent
// is a plain record; the behaviour worth testing is BusinessAppWorkflowEngineWaitingStateTests
// below, which exercises the real engine end to end.

/// <summary>
/// Tests for BusinessAppProcessManager integration with waiting states.
/// Validates that BuildEnvelope produces correct output for waiting state configurations.
/// </summary>
public class BusinessAppWorkflowEngineWaitingStateTests : IDisposable
{
    private readonly string _testSeedDir;
    private readonly BusinessAppProcessManager _engine;
    private readonly Mock<IWebHostEnvironment> _mockEnv;

    public BusinessAppWorkflowEngineWaitingStateTests()
    {
        _testSeedDir = Path.Combine(Directory.GetCurrentDirectory(), $"test-seeds-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testSeedDir);
        Directory.CreateDirectory(Path.Combine(_testSeedDir, "service-blueprints"));

        SeedTestWorkflow();

        _mockEnv = new Mock<IWebHostEnvironment>();
        _mockEnv.Setup(e => e.ContentRootPath).Returns(_testSeedDir);

        var logger = new Mock<ILogger<BusinessAppProcessManager>>();
        var sanitizer = new Mock<IServiceContentSanitizer>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string?>())).Returns<string?>(h => h ?? string.Empty);
        _engine = new BusinessAppProcessManager(logger.Object, _mockEnv.Object, sanitizer.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testSeedDir))
        {
            Directory.Delete(_testSeedDir, recursive: true);
        }
    }

    private void SeedTestWorkflow()
    {
        var seedsDir = Path.Combine(_testSeedDir, "service-blueprints");

        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "test-waiting-workflow",
            DisplayName = "Test Waiting Workflow",
            Version = 1,
            InitialStage = "enter-details",
            RequestPolicy = "single",
            Stages = new[]
            {
                new StageDefinition
                {
                    StageKey = "enter-details",
                    DisplayName = "Enter Details",
                    Components = Array.Empty<Component>(),
                    Routes = new[]
                    {
                        new ServiceBlueprintRouteDefinition { Id = "enter-details--submit--to-processing", Target = "to-processing", Trigger = "submit" }
                    }
                },
                new StageDefinition
                {
                    StageKey = "processing",
                    DisplayName = "Processing",
                    Components = new Component[]
                    {
                        new WaitingComponent
                        {
                            Content = "We are reviewing your submission.",
                            ExpectedWaitSeconds = 60,
                            PollIntervalMs = 2000,
                            AllowDefer = true,
                            DeferMessage = "You can come back via My Applications."
                        }
                    },
                    Routes = new[]
                    {
                        new ServiceBlueprintRouteDefinition { Id = "processing--complete--to-done", Target = "to-done", Trigger = "complete" }
                    }
                },
                new StageDefinition
                {
                    StageKey = "done",
                    DisplayName = "Done",
                    Components = new Component[]
                    {
                        new PanelComponent { Heading = "Complete" }
                    }
                }
            },
            // Every stage route must target a gateway, never another stage directly — even
            // this trivial pass-through shape needs one gateway per handoff (see Wayfinder's
            // reference-service-blueprint-contract.md "gateway routing rule").
            Gateways = new[]
            {
                new ServiceBlueprintGatewayDefinition
                {
                    Key = "to-processing",
                    DisplayName = "To processing",
                    GatewayType = "Split",
                    Routes = new[] { new ServiceBlueprintRouteDefinition { Id = "to-processing--continue--processing", Target = "processing", Trigger = "continue" } }
                },
                new ServiceBlueprintGatewayDefinition
                {
                    Key = "to-done",
                    DisplayName = "To done",
                    GatewayType = "Split",
                    Routes = new[] { new ServiceBlueprintRouteDefinition { Id = "to-done--continue--done", Target = "done", Trigger = "continue" } }
                }
            }
        };

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(
            Path.Combine(seedsDir, "test-waiting-workflow.json"),
            JsonSerializer.Serialize(workflow, jsonOptions));
    }

    [Fact]
    public void WhenCurrentStateIsWaiting_StepContentWaitingConfigIsPopulated()
    {
        _engine.ResetAll();

        var firstResponse = _engine.GetCurrent("test-waiting-workflow", "tenant1", "user1");
        _engine.Advance(firstResponse.InstanceId, "tenant1", "user1", "submit", expectedStateVersion: 0, fieldValues: new Dictionary<string, object?>());

        var result = _engine.GetCurrent("test-waiting-workflow", "tenant1", "user1");

        result.Render.Should().NotBeNull();
        var waitingComponent = result.Render!.Components.FirstOrDefault(c =>
            string.Equals(c.Type, "waiting", StringComparison.OrdinalIgnoreCase));
        waitingComponent.Should().NotBeNull();
    }

    [Fact]
    public void WhenCurrentStateIsWaiting_WaitingConfigMessageMatchesDefinition()
    {
        _engine.ResetAll();

        var firstResponse = _engine.GetCurrent("test-waiting-workflow", "tenant1", "user1");
        _engine.Advance(firstResponse.InstanceId, "tenant1", "user1", "submit", expectedStateVersion: 0, fieldValues: new Dictionary<string, object?>());

        var result = _engine.GetCurrent("test-waiting-workflow", "tenant1", "user1");

        var waitingComponent = result.Render!.Components.FirstOrDefault(c =>
            string.Equals(c.Type, "waiting", StringComparison.OrdinalIgnoreCase));
        waitingComponent!.Content.Should().Be("We are reviewing your submission.");
    }

    [Fact]
    public void WhenCurrentStateIsWaiting_WaitingConfigExpectedWaitSecondsMatchesDefinition()
    {
        _engine.ResetAll();

        var firstResponse = _engine.GetCurrent("test-waiting-workflow", "tenant1", "user1");
        _engine.Advance(firstResponse.InstanceId, "tenant1", "user1", "submit", expectedStateVersion: 0, fieldValues: new Dictionary<string, object?>());

        var result = _engine.GetCurrent("test-waiting-workflow", "tenant1", "user1");

        var waitingComponent = result.Render!.Components.FirstOrDefault(c =>
            string.Equals(c.Type, "waiting", StringComparison.OrdinalIgnoreCase));
        waitingComponent!.ExpectedWaitSeconds.Should().Be(60);
    }

    [Fact]
    public void WhenCurrentStateIsWaiting_WaitingConfigPollIntervalMsMatchesDefinition()
    {
        _engine.ResetAll();

        var firstResponse = _engine.GetCurrent("test-waiting-workflow", "tenant1", "user1");
        _engine.Advance(firstResponse.InstanceId, "tenant1", "user1", "submit", expectedStateVersion: 0, fieldValues: new Dictionary<string, object?>());

        var result = _engine.GetCurrent("test-waiting-workflow", "tenant1", "user1");

        var waitingComponent = result.Render!.Components.FirstOrDefault(c =>
            string.Equals(c.Type, "waiting", StringComparison.OrdinalIgnoreCase));
        waitingComponent!.PollIntervalMs.Should().Be(2000);
    }

    [Fact]
    public void WhenCurrentStateIsWaiting_WaitingConfigAllowDeferMatchesDefinition()
    {
        _engine.ResetAll();

        var firstResponse = _engine.GetCurrent("test-waiting-workflow", "tenant1", "user1");
        _engine.Advance(firstResponse.InstanceId, "tenant1", "user1", "submit", expectedStateVersion: 0, fieldValues: new Dictionary<string, object?>());

        var result = _engine.GetCurrent("test-waiting-workflow", "tenant1", "user1");

        var waitingComponent = result.Render!.Components.FirstOrDefault(c =>
            string.Equals(c.Type, "waiting", StringComparison.OrdinalIgnoreCase));
        waitingComponent!.AllowDefer.Should().BeTrue();
    }

    [Fact]
    public void WhenCurrentStateIsWaiting_WaitingConfigDeferMessageMatchesDefinition()
    {
        _engine.ResetAll();

        var firstResponse = _engine.GetCurrent("test-waiting-workflow", "tenant1", "user1");
        _engine.Advance(firstResponse.InstanceId, "tenant1", "user1", "submit", expectedStateVersion: 0, fieldValues: new Dictionary<string, object?>());

        var result = _engine.GetCurrent("test-waiting-workflow", "tenant1", "user1");

        var waitingComponent = result.Render!.Components.FirstOrDefault(c =>
            string.Equals(c.Type, "waiting", StringComparison.OrdinalIgnoreCase));
        waitingComponent!.DeferMessage.Should().Be("You can come back via My Applications.");
    }

    [Fact]
    public void WhenCurrentStateIsWaiting_PollAfterMsOnEnvelopeEqualsWaitingConfigPollIntervalMs()
    {
        _engine.ResetAll();

        var firstResponse = _engine.GetCurrent("test-waiting-workflow", "tenant1", "user1");
        _engine.Advance(firstResponse.InstanceId, "tenant1", "user1", "submit", expectedStateVersion: 0, fieldValues: new Dictionary<string, object?>());

        var result = _engine.GetCurrent("test-waiting-workflow", "tenant1", "user1");

        result.PollAfterMs.Should().Be(2000);
    }

    [Fact]
    public void WhenCurrentStateIsWaiting_ResponseStateIsRender()
    {
        _engine.ResetAll();

        var firstResponse = _engine.GetCurrent("test-waiting-workflow", "tenant1", "user1");
        _engine.Advance(firstResponse.InstanceId, "tenant1", "user1", "submit", expectedStateVersion: 0, fieldValues: new Dictionary<string, object?>());

        var result = _engine.GetCurrent("test-waiting-workflow", "tenant1", "user1");

        result.ResponseState.Should().Be("defer");
    }

    [Fact]
    public void WhenCurrentStateIsWaiting_StepTypeIsWaiting()
    {
        _engine.ResetAll();

        var firstResponse = _engine.GetCurrent("test-waiting-workflow", "tenant1", "user1");
        _engine.Advance(firstResponse.InstanceId, "tenant1", "user1", "submit", expectedStateVersion: 0, fieldValues: new Dictionary<string, object?>());

        var result = _engine.GetCurrent("test-waiting-workflow", "tenant1", "user1");

        result.Render!.StepType.Should().Be("status-timeline");
    }

    [Fact]
    public void WhenCurrentStateIsNotWaiting_PollAfterMsIsNull()
    {
        _engine.ResetAll();

        var result = _engine.GetCurrent("test-waiting-workflow", "tenant1", "user1");

        result.PollAfterMs.Should().BeNull();
    }

    [Fact]
    public void WhenCurrentStateIsNotWaiting_StepContentWaitingConfigIsNull()
    {
        _engine.ResetAll();

        var result = _engine.GetCurrent("test-waiting-workflow", "tenant1", "user1");

        result.Render.Should().NotBeNull();
        var waitingComponent = result.Render!.Components.FirstOrDefault(c =>
            string.Equals(c.Type, "waiting", StringComparison.OrdinalIgnoreCase));
        waitingComponent.Should().BeNull();
    }

    [Fact]
    public void AdvancingFromWaitingStateToConfirmation_WorksNormally()
    {
        _engine.ResetAll();

        var firstResponse = _engine.GetCurrent("test-waiting-workflow", "tenant1", "user1");
        _engine.Advance(firstResponse.InstanceId, "tenant1", "user1", "submit", expectedStateVersion: 0, fieldValues: new Dictionary<string, object?>());
        var waitingResponse = _engine.GetCurrent("test-waiting-workflow", "tenant1", "user1");

        _engine.Advance(waitingResponse.InstanceId, "tenant1", "user1", "complete", expectedStateVersion: 1, fieldValues: new Dictionary<string, object?>());
        var finalResponse = _engine.GetCurrent("test-waiting-workflow", "tenant1", "user1");

        finalResponse.Render!.StepType.Should().Be("confirmation");
        finalResponse.ResponseState.Should().Be("complete");
    }

    [Fact]
    public void WaitingConfigWithAllowDeferFalse_EnvelopeStillHasResponseStateRender()
    {
        var testSeedDir = Path.Combine(Directory.GetCurrentDirectory(), $"test-seeds-no-defer-{Guid.NewGuid()}");
        Directory.CreateDirectory(testSeedDir);
        Directory.CreateDirectory(Path.Combine(testSeedDir, "service-blueprints"));

        try
        {
            var seedsDir = Path.Combine(testSeedDir, "service-blueprints");
            var workflow = new ServiceBlueprint
            {
                DefinitionKey = "test-no-defer",
                DisplayName = "Test No Defer",
                Version = 1,
                InitialStage = "processing",
                RequestPolicy = "single",
                Stages = new[]
                {
                    new StageDefinition
                    {
                        StageKey = "processing",
                        DisplayName = "Processing",
                        Components = new Component[]
                        {
                            new WaitingComponent
                            {
                                Content = "Wait here",
                                ExpectedWaitSeconds = 30,
                                PollIntervalMs = 3000,
                                AllowDefer = false,
                                DeferMessage = null
                            }
                        }
                    }
                }
            };

            File.WriteAllText(
                Path.Combine(seedsDir, "test-no-defer.json"),
                JsonSerializer.Serialize(workflow, new JsonSerializerOptions { WriteIndented = true }));

            var mockEnv = new Mock<IWebHostEnvironment>();
            mockEnv.Setup(e => e.ContentRootPath).Returns(testSeedDir);
            var logger = new Mock<ILogger<BusinessAppProcessManager>>();
            var sanitizer = new Mock<IServiceContentSanitizer>();
            sanitizer.Setup(s => s.Sanitize(It.IsAny<string?>())).Returns<string?>(h => h ?? string.Empty);
            var engine = new BusinessAppProcessManager(logger.Object, mockEnv.Object, sanitizer.Object);
            engine.ResetAll();

            var result = engine.GetCurrent("test-no-defer", "tenant1", "user1");

            result.ResponseState.Should().Be("defer");
        }
        finally
        {
            if (Directory.Exists(testSeedDir))
            {
                Directory.Delete(testSeedDir, recursive: true);
            }
        }
    }

    [Fact]
    public void WaitingConfigWithNullDeferMessage_WaitingConfigDeferMessageIsNull()
    {
        var testSeedDir = Path.Combine(Directory.GetCurrentDirectory(), $"test-seeds-null-defer-{Guid.NewGuid()}");
        Directory.CreateDirectory(testSeedDir);
        Directory.CreateDirectory(Path.Combine(testSeedDir, "service-blueprints"));

        try
        {
            var seedsDir = Path.Combine(testSeedDir, "service-blueprints");
            var workflow = new ServiceBlueprint
            {
                DefinitionKey = "test-null-defer",
                DisplayName = "Test Null Defer",
                Version = 1,
                InitialStage = "processing",
                RequestPolicy = "single",
                Stages = new[]
                {
                    new StageDefinition
                    {
                        StageKey = "processing",
                        DisplayName = "Processing",
                        Components = new Component[]
                        {
                            new WaitingComponent
                            {
                                Content = "Wait here",
                                ExpectedWaitSeconds = 30,
                                PollIntervalMs = 3000,
                                AllowDefer = true,
                                DeferMessage = null
                            }
                        }
                    }
                }
            };

            File.WriteAllText(
                Path.Combine(seedsDir, "test-null-defer.json"),
                JsonSerializer.Serialize(workflow, new JsonSerializerOptions { WriteIndented = true }));

            var mockEnv = new Mock<IWebHostEnvironment>();
            mockEnv.Setup(e => e.ContentRootPath).Returns(testSeedDir);
            var logger = new Mock<ILogger<BusinessAppProcessManager>>();
            var sanitizer2 = new Mock<IServiceContentSanitizer>();
            sanitizer2.Setup(s => s.Sanitize(It.IsAny<string?>())).Returns<string?>(h => h ?? string.Empty);
            var engine = new BusinessAppProcessManager(logger.Object, mockEnv.Object, sanitizer2.Object);
            engine.ResetAll();

            var result = engine.GetCurrent("test-null-defer", "tenant1", "user1");

            var waitingComponent = result.Render!.Components.FirstOrDefault(c =>
                string.Equals(c.Type, "waiting", StringComparison.OrdinalIgnoreCase));
            waitingComponent.Should().NotBeNull();
            waitingComponent!.DeferMessage.Should().BeNull();
        }
        finally
        {
            if (Directory.Exists(testSeedDir))
            {
                Directory.Delete(testSeedDir, recursive: true);
            }
        }
    }
}
