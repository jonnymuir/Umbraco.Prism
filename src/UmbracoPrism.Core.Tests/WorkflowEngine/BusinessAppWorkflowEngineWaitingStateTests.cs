using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.MockBusinessApp.Services;
using UmbracoPrism.Shared.Builders;
using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.Core.Tests.WorkflowEngine;


/// <summary>
/// Tests for the WaitWith() fluent builder method on WorkflowStateBuilder.
/// Validates that the builder correctly populates WaitingConfig and StepType.
/// </summary>
public class WaitWithBuilderTests
{
    [Fact]
    public void WaitWith_SetsComponentTypeToWaiting()
    {
        var workflow = new WorkflowDefinitionBuilder()
            .Key("test")
            .DisplayName("Test")
            .Version(1)
            .StartsAt("waiting")
            .AddState("waiting", s => s
                .DisplayName("Waiting")
                .WaitWith(
                    message: "Processing...",
                    expectedWaitSeconds: 30))
            .Build();

        var state = workflow.States.First();
        var waitingComponent = state.Components.FirstOrDefault(c => 
            string.Equals(c.Type, "waiting", StringComparison.OrdinalIgnoreCase));
        waitingComponent.Should().NotBeNull();
    }

    [Fact]
    public void WaitWith_PopulatesWaitingComponentContent()
    {
        var workflow = new WorkflowDefinitionBuilder()
            .Key("test")
            .DisplayName("Test")
            .Version(1)
            .StartsAt("waiting")
            .AddState("waiting", s => s
                .DisplayName("Waiting")
                .WaitWith(
                    message: "Please hold tight.",
                    expectedWaitSeconds: 30))
            .Build();

        var state = workflow.States.First();
        var waitingComponent = state.Components.FirstOrDefault(c => 
            string.Equals(c.Type, "waiting", StringComparison.OrdinalIgnoreCase));
        waitingComponent.Should().NotBeNull();
        waitingComponent!.Content.Should().Be("Please hold tight.");
    }

    [Fact]
    public void WaitWith_PopulatesWaitingComponentExpectedWaitSeconds()
    {
        var workflow = new WorkflowDefinitionBuilder()
            .Key("test")
            .DisplayName("Test")
            .Version(1)
            .StartsAt("waiting")
            .AddState("waiting", s => s
                .DisplayName("Waiting")
                .WaitWith(
                    message: "Processing...",
                    expectedWaitSeconds: 120))
            .Build();

        var state = workflow.States.First();
        var waitingComponent = state.Components.FirstOrDefault(c => 
            string.Equals(c.Type, "waiting", StringComparison.OrdinalIgnoreCase));
        waitingComponent.Should().NotBeNull();
        waitingComponent!.ExpectedWaitSeconds.Should().Be(120);
    }

    [Fact]
    public void WaitWith_UsesDefaultPollIntervalMsWhenNotSpecified()
    {
        var workflow = new WorkflowDefinitionBuilder()
            .Key("test")
            .DisplayName("Test")
            .Version(1)
            .StartsAt("waiting")
            .AddState("waiting", s => s
                .DisplayName("Waiting")
                .WaitWith(
                    message: "Processing...",
                    expectedWaitSeconds: 30))
            .Build();

        var state = workflow.States.First();
        var waitingComponent = state.Components.FirstOrDefault(c => 
            string.Equals(c.Type, "waiting", StringComparison.OrdinalIgnoreCase));
        waitingComponent.Should().NotBeNull();
        waitingComponent!.PollIntervalMs.Should().Be(3000);
    }

    [Fact]
    public void WaitWith_UsesProvidedPollIntervalMsWhenSpecified()
    {
        var workflow = new WorkflowDefinitionBuilder()
            .Key("test")
            .DisplayName("Test")
            .Version(1)
            .StartsAt("waiting")
            .AddState("waiting", s => s
                .DisplayName("Waiting")
                .WaitWith(
                    message: "Processing...",
                    expectedWaitSeconds: 30,
                    pollIntervalMs: 5000))
            .Build();

        var state = workflow.States.First();
        var waitingComponent = state.Components.FirstOrDefault(c => 
            string.Equals(c.Type, "waiting", StringComparison.OrdinalIgnoreCase));
        waitingComponent.Should().NotBeNull();
        waitingComponent!.PollIntervalMs.Should().Be(5000);
    }

    [Fact]
    public void WaitWith_SetsAllowDeferTrueByDefault()
    {
        var workflow = new WorkflowDefinitionBuilder()
            .Key("test")
            .DisplayName("Test")
            .Version(1)
            .StartsAt("waiting")
            .AddState("waiting", s => s
                .DisplayName("Waiting")
                .WaitWith(
                    message: "Processing...",
                    expectedWaitSeconds: 30))
            .Build();

        var state = workflow.States.First();
        var waitingComponent = state.Components.FirstOrDefault(c => 
            string.Equals(c.Type, "waiting", StringComparison.OrdinalIgnoreCase));
        waitingComponent.Should().NotBeNull();
        waitingComponent!.AllowDefer.Should().BeTrue();
    }

    [Fact]
    public void WaitWith_RespectsAllowDeferFalse()
    {
        var workflow = new WorkflowDefinitionBuilder()
            .Key("test")
            .DisplayName("Test")
            .Version(1)
            .StartsAt("waiting")
            .AddState("waiting", s => s
                .DisplayName("Waiting")
                .WaitWith(
                    message: "Processing...",
                    expectedWaitSeconds: 30,
                    allowDefer: false))
            .Build();

        var state = workflow.States.First();
        var waitingComponent = state.Components.FirstOrDefault(c => 
            string.Equals(c.Type, "waiting", StringComparison.OrdinalIgnoreCase));
        waitingComponent.Should().NotBeNull();
        waitingComponent!.AllowDefer.Should().BeFalse();
    }

    [Fact]
    public void WaitWith_SetsDeferMessageWhenProvided()
    {
        var workflow = new WorkflowDefinitionBuilder()
            .Key("test")
            .DisplayName("Test")
            .Version(1)
            .StartsAt("waiting")
            .AddState("waiting", s => s
                .DisplayName("Waiting")
                .WaitWith(
                    message: "Processing...",
                    expectedWaitSeconds: 30,
                    deferMessage: "Come back later via My Applications."))
            .Build();

        var state = workflow.States.First();
        var waitingComponent = state.Components.FirstOrDefault(c => 
            string.Equals(c.Type, "waiting", StringComparison.OrdinalIgnoreCase));
        waitingComponent.Should().NotBeNull();
        waitingComponent!.DeferMessage.Should().Be("Come back later via My Applications.");
    }

    [Fact]
    public void WaitWith_LeavesDeferMessageNullWhenNotProvided()
    {
        var workflow = new WorkflowDefinitionBuilder()
            .Key("test")
            .DisplayName("Test")
            .Version(1)
            .StartsAt("waiting")
            .AddState("waiting", s => s
                .DisplayName("Waiting")
                .WaitWith(
                    message: "Processing...",
                    expectedWaitSeconds: 30))
            .Build();

        var state = workflow.States.First();
        var waitingComponent = state.Components.FirstOrDefault(c => 
            string.Equals(c.Type, "waiting", StringComparison.OrdinalIgnoreCase));
        waitingComponent.Should().NotBeNull();
        waitingComponent!.DeferMessage.Should().BeNull();
    }
}

/// <summary>
/// Tests for the builder fluent API with waiting states.
/// </summary>
public class WaitWithBuilderFluentTests
{
    [Fact]
    public void WaitWith_IsFluentReturnsSameBuilder()
    {
        var builder = new WorkflowDefinitionBuilder();
        WorkflowStateBuilder? capturedBuilder = null;

        builder
            .Key("test")
            .DisplayName("Test")
            .Version(1)
            .StartsAt("waiting")
            .AddState("waiting", s =>
            {
                capturedBuilder = s;
                var returned = s.WaitWith(
                    message: "Processing...",
                    expectedWaitSeconds: 30);
                returned.Should().BeSameAs(capturedBuilder);
            });
    }

    [Fact]
    public void FullWorkflowBuiltWithWaitWith_HasCorrectStateCountAndTransitions()
    {
        var workflow = new WorkflowDefinitionBuilder()
            .Key("test-workflow")
            .DisplayName("Test Workflow")
            .Version(1)
            .StartsAt("start")
            .InstancePolicy("single")
            .AddState("start", s => s
                .DisplayName("Start")
                .AddFieldset(new[] 
                {
                    new FieldFile { FieldKey = "name", Label = "Name", FieldType = "text", Required = true }
                }))
            .AddState("processing", s => s
                .DisplayName("Processing")
                .WaitWith(
                    message: "Please wait...",
                    expectedWaitSeconds: 60))
            .AddState("done", s => s
                .DisplayName("Done")
                .AddContent("panel", "Complete"))
            .AddTransition("start", "processing", "submit")
            .AddTransition("processing", "done", "complete")
            .Build();

        workflow.States.Should().HaveCount(3);
        workflow.Transitions.Should().HaveCount(2);
        var processingState = workflow.States.First(s => s.StateKey == "processing");
        var waitingComponent = processingState.Components.FirstOrDefault(c => 
            string.Equals(c.Type, "waiting", StringComparison.OrdinalIgnoreCase));
        waitingComponent.Should().NotBeNull();
    }
}

/// <summary>
/// Tests for BusinessAppWorkflowEngine integration with waiting states.
/// Validates that BuildEnvelope produces correct output for waiting state configurations.
/// </summary>
public class BusinessAppWorkflowEngineWaitingStateTests : IDisposable
{
    private readonly string _testSeedDir;
    private readonly BusinessAppWorkflowEngine _engine;
    private readonly Mock<IWebHostEnvironment> _mockEnv;

    public BusinessAppWorkflowEngineWaitingStateTests()
    {
        _testSeedDir = Path.Combine(Directory.GetCurrentDirectory(), $"test-seeds-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testSeedDir);
        Directory.CreateDirectory(Path.Combine(_testSeedDir, "workflow-seeds"));

        SeedTestWorkflow();

        _mockEnv = new Mock<IWebHostEnvironment>();
        _mockEnv.Setup(e => e.ContentRootPath).Returns(_testSeedDir);

        var logger = new Mock<ILogger<BusinessAppWorkflowEngine>>();
        _engine = new BusinessAppWorkflowEngine(logger.Object, _mockEnv.Object);
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
        var seedsDir = Path.Combine(_testSeedDir, "workflow-seeds");

        var workflow = new WorkflowDefinitionFile
        {
            DefinitionKey = "test-waiting-workflow",
            DisplayName = "Test Waiting Workflow",
            Version = 1,
            InitialState = "enter-details",
            InstancePolicy = "single",
            States = new[]
            {
                new StepDefinition
                {
                    StateKey = "enter-details",
                    DisplayName = "Enter Details",
                    Components = Array.Empty<PrismComponentDefinition>()
                },
                new StepDefinition
                {
                    StateKey = "processing",
                    DisplayName = "Processing",
                    Components = new[]
                    {
                        new PrismComponentDefinition
                        {
                            Type = "waiting",
                            Content = "We are reviewing your submission.",
                            ExpectedWaitSeconds = 60,
                            PollIntervalMs = 2000,
                            AllowDefer = true,
                            DeferMessage = "You can come back via My Applications."
                        }
                    }
                },
                new StepDefinition
                {
                    StateKey = "done",
                    DisplayName = "Done",
                    Components = Array.Empty<PrismComponentDefinition>()
                }
            },
            Transitions = new[]
            {
                new WorkflowTransitionFile
                {
                    FromState = "enter-details",
                    ToState = "processing",
                    Action = "submit",
                    RequiresRole = null
                },
                new WorkflowTransitionFile
                {
                    FromState = "processing",
                    ToState = "done",
                    Action = "complete",
                    RequiresRole = null
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

        result.ResponseState.Should().Be("render");
    }

    [Fact]
    public void WhenCurrentStateIsWaiting_StepTypeIsWaiting()
    {
        _engine.ResetAll();

        var firstResponse = _engine.GetCurrent("test-waiting-workflow", "tenant1", "user1");
        _engine.Advance(firstResponse.InstanceId, "tenant1", "user1", "submit", expectedStateVersion: 0, fieldValues: new Dictionary<string, object?>());

        var result = _engine.GetCurrent("test-waiting-workflow", "tenant1", "user1");

        result.Render!.StepType.Should().Be("waiting");
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
        Directory.CreateDirectory(Path.Combine(testSeedDir, "workflow-seeds"));

        try
        {
            var seedsDir = Path.Combine(testSeedDir, "workflow-seeds");
            var workflow = new WorkflowDefinitionFile
            {
                DefinitionKey = "test-no-defer",
                DisplayName = "Test No Defer",
                Version = 1,
                InitialState = "processing",
                InstancePolicy = "single",
                States = new[]
                {
                    new StepDefinition
                    {
                        StateKey = "processing",
                        DisplayName = "Processing",
                        Components = new[]
                        {
                            new PrismComponentDefinition
                            {
                                Type = "waiting",
                                Content = "Wait here",
                                ExpectedWaitSeconds = 30,
                                PollIntervalMs = 3000,
                                AllowDefer = false,
                                DeferMessage = null
                            }
                        }
                    }
                },
                Transitions = Array.Empty<WorkflowTransitionFile>()
            };

            File.WriteAllText(
                Path.Combine(seedsDir, "test-no-defer.json"),
                JsonSerializer.Serialize(workflow, new JsonSerializerOptions { WriteIndented = true }));

            var mockEnv = new Mock<IWebHostEnvironment>();
            mockEnv.Setup(e => e.ContentRootPath).Returns(testSeedDir);
            var logger = new Mock<ILogger<BusinessAppWorkflowEngine>>();
            var engine = new BusinessAppWorkflowEngine(logger.Object, mockEnv.Object);
            engine.ResetAll();

            var result = engine.GetCurrent("test-no-defer", "tenant1", "user1");

            result.ResponseState.Should().Be("render");
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
        Directory.CreateDirectory(Path.Combine(testSeedDir, "workflow-seeds"));

        try
        {
            var seedsDir = Path.Combine(testSeedDir, "workflow-seeds");
            var workflow = new WorkflowDefinitionFile
            {
                DefinitionKey = "test-null-defer",
                DisplayName = "Test Null Defer",
                Version = 1,
                InitialState = "processing",
                InstancePolicy = "single",
                States = new[]
                {
                    new StepDefinition
                    {
                        StateKey = "processing",
                        DisplayName = "Processing",
                        Components = new[]
                        {
                            new PrismComponentDefinition
                            {
                                Type = "waiting",
                                Content = "Wait here",
                                ExpectedWaitSeconds = 30,
                                PollIntervalMs = 3000,
                                AllowDefer = true,
                                DeferMessage = null
                            }
                        }
                    }
                },
                Transitions = Array.Empty<WorkflowTransitionFile>()
            };

            File.WriteAllText(
                Path.Combine(seedsDir, "test-null-defer.json"),
                JsonSerializer.Serialize(workflow, new JsonSerializerOptions { WriteIndented = true }));

            var mockEnv = new Mock<IWebHostEnvironment>();
            mockEnv.Setup(e => e.ContentRootPath).Returns(testSeedDir);
            var logger = new Mock<ILogger<BusinessAppWorkflowEngine>>();
            var engine = new BusinessAppWorkflowEngine(logger.Object, mockEnv.Object);
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
