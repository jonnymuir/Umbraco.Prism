using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.MockBusinessApp.Services;
using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.Core.Tests.WorkflowEngine;

public class BusinessAppWorkflowEngineInstancePolicyTests : IDisposable
{
    private readonly string _testSeedDir;
    private readonly BusinessAppWorkflowEngine _engine;
    private readonly Mock<IWebHostEnvironment> _mockEnv;

    public BusinessAppWorkflowEngineInstancePolicyTests()
    {
        _testSeedDir = Path.Combine(Directory.GetCurrentDirectory(), $"test-seeds-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testSeedDir);
        Directory.CreateDirectory(Path.Combine(_testSeedDir, "workflow-seeds"));

        SeedTestWorkflows();

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

    private void SeedTestWorkflows()
    {
        var seedsDir = Path.Combine(_testSeedDir, "workflow-seeds");

        var singlePolicyWorkflow = new WorkflowDefinitionFile
        {
            DefinitionKey = "test-workflow-single",
            DisplayName = "Test Workflow Single",
            Version = 1,
            InitialState = "step-1",
            InstancePolicy = "single",
            States = new[]
            {
                new StepDefinition
                {
                    StateKey = "step-1",
                    DisplayName = "Step 1",
                    Components = Array.Empty<PrismComponentDefinition>()
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
                    FromState = "step-1",
                    ToState = "done",
                    Action = "submit",
                    RequiresRole = null
                }
            }
        };

        var multiplePolicyWorkflow = new WorkflowDefinitionFile
        {
            DefinitionKey = "test-workflow-multiple",
            DisplayName = "Test Workflow Multiple",
            Version = 1,
            InitialState = "step-1",
            InstancePolicy = "multiple",
            States = new[]
            {
                new StepDefinition
                {
                    StateKey = "step-1",
                    DisplayName = "Step 1",
                    Components = Array.Empty<PrismComponentDefinition>()
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
                    FromState = "step-1",
                    ToState = "done",
                    Action = "submit",
                    RequiresRole = null
                }
            }
        };

        var promptPolicyWorkflow = new WorkflowDefinitionFile
        {
            DefinitionKey = "test-workflow-prompt",
            DisplayName = "Test Workflow Prompt",
            Version = 1,
            InitialState = "step-1",
            InstancePolicy = "prompt",
            States = new[]
            {
                new StepDefinition
                {
                    StateKey = "step-1",
                    DisplayName = "Step 1",
                    Components = Array.Empty<PrismComponentDefinition>()
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
                    FromState = "step-1",
                    ToState = "done",
                    Action = "submit",
                    RequiresRole = null
                }
            }
        };

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(
            Path.Combine(seedsDir, "test-workflow-single.json"),
            JsonSerializer.Serialize(singlePolicyWorkflow, jsonOptions));
        File.WriteAllText(
            Path.Combine(seedsDir, "test-workflow-multiple.json"),
            JsonSerializer.Serialize(multiplePolicyWorkflow, jsonOptions));
        File.WriteAllText(
            Path.Combine(seedsDir, "test-workflow-prompt.json"),
            JsonSerializer.Serialize(promptPolicyWorkflow, jsonOptions));
    }

    // -----------------------------------------------------------------------
    // "single" policy tests (existing behaviour — regression tests)
    // -----------------------------------------------------------------------

    [Fact]
    public void SinglePolicy_FirstCall_CreatesNewInstance()
    {
        _engine.ResetAll();

        var result = _engine.GetCurrent("test-workflow-single", "tenant1", "user1");

        result.ResponseState.Should().Be("render");
        result.InstanceId.Should().NotBeNullOrEmpty();
        result.InstancePolicy.Should().Be("single");
    }

    [Fact]
    public void SinglePolicy_SecondCallSameUser_ResumesSameInstance()
    {
        _engine.ResetAll();

        var first = _engine.GetCurrent("test-workflow-single", "tenant1", "user1");
        var second = _engine.GetCurrent("test-workflow-single", "tenant1", "user1");

        second.InstanceId.Should().Be(first.InstanceId);
        second.ResponseState.Should().Be("render");
    }

    [Fact]
    public void SinglePolicy_DifferentUser_GetsDifferentInstance()
    {
        _engine.ResetAll();

        var user1Result = _engine.GetCurrent("test-workflow-single", "tenant1", "user1");
        var user2Result = _engine.GetCurrent("test-workflow-single", "tenant1", "user2");

        user2Result.InstanceId.Should().NotBe(user1Result.InstanceId);
        user2Result.ResponseState.Should().Be("render");
    }

    [Fact]
    public void SinglePolicy_DifferentTenant_GetsDifferentInstance()
    {
        _engine.ResetAll();

        var tenant1Result = _engine.GetCurrent("test-workflow-single", "tenant1", "user1");
        var tenant2Result = _engine.GetCurrent("test-workflow-single", "tenant2", "user1");

        tenant2Result.InstanceId.Should().NotBe(tenant1Result.InstanceId);
        tenant2Result.ResponseState.Should().Be("render");
    }

    // -----------------------------------------------------------------------
    // "multiple" policy tests
    // -----------------------------------------------------------------------

    [Fact]
    public void MultiplePolicy_FirstCall_CreatesNewInstance()
    {
        _engine.ResetAll();

        var result = _engine.GetCurrent("test-workflow-multiple", "tenant1", "user1");

        result.ResponseState.Should().Be("render");
        result.InstanceId.Should().NotBeNullOrEmpty();
        result.InstancePolicy.Should().Be("multiple");
    }

    [Fact]
    public void MultiplePolicy_SecondCallSameUser_CreatesDifferentInstance()
    {
        _engine.ResetAll();

        var first = _engine.GetCurrent("test-workflow-multiple", "tenant1", "user1");
        var second = _engine.GetCurrent("test-workflow-multiple", "tenant1", "user1");

        second.InstanceId.Should().NotBe(first.InstanceId);
        second.ResponseState.Should().Be("render");
    }

    [Fact]
    public void MultiplePolicy_ThirdCall_CreatesYetAnotherInstance()
    {
        _engine.ResetAll();

        var first = _engine.GetCurrent("test-workflow-multiple", "tenant1", "user1");
        var second = _engine.GetCurrent("test-workflow-multiple", "tenant1", "user1");
        var third = _engine.GetCurrent("test-workflow-multiple", "tenant1", "user1");

        third.InstanceId.Should().NotBe(first.InstanceId);
        third.InstanceId.Should().NotBe(second.InstanceId);
        third.ResponseState.Should().Be("render");
    }

    [Fact]
    public void MultiplePolicy_WithInstanceIdParam_CanResumeNamedInstance()
    {
        _engine.ResetAll();

        var first = _engine.GetCurrent("test-workflow-multiple", "tenant1", "user1");
        var firstInstanceId = first.InstanceId;

        var second = _engine.GetCurrent("test-workflow-multiple", "tenant1", "user1");
        second.InstanceId.Should().NotBe(firstInstanceId);

        var resumed = _engine.GetCurrent("test-workflow-multiple", "tenant1", "user1", instanceId: firstInstanceId);

        resumed.InstanceId.Should().Be(firstInstanceId);
        resumed.ResponseState.Should().Be("render");
    }

    // -----------------------------------------------------------------------
    // "prompt" policy tests
    // -----------------------------------------------------------------------

    [Fact]
    public void PromptPolicy_FirstCallNoExistingInstance_CreatesNewInstance()
    {
        _engine.ResetAll();

        var result = _engine.GetCurrent("test-workflow-prompt", "tenant1", "user1");

        result.ResponseState.Should().Be("render");
        result.InstanceId.Should().NotBeNullOrEmpty();
        result.InstancePolicy.Should().Be("prompt");
    }

    [Fact]
    public void PromptPolicy_SecondCallNoAction_ReturnsInstancePicker()
    {
        _engine.ResetAll();

        var first = _engine.GetCurrent("test-workflow-prompt", "tenant1", "user1");
        var second = _engine.GetCurrent("test-workflow-prompt", "tenant1", "user1");

        second.ResponseState.Should().Be("instance_picker");
        second.InstanceId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void PromptPolicy_ActionResume_ReturnsExistingInstance()
    {
        _engine.ResetAll();

        var first = _engine.GetCurrent("test-workflow-prompt", "tenant1", "user1");
        var firstInstanceId = first.InstanceId;

        var resumed = _engine.GetCurrent("test-workflow-prompt", "tenant1", "user1", action: "resume");

        resumed.InstanceId.Should().Be(firstInstanceId);
        resumed.ResponseState.Should().Be("render");
    }

    [Fact]
    public void PromptPolicy_ActionStartNew_CreatesFreshInstance()
    {
        _engine.ResetAll();

        var first = _engine.GetCurrent("test-workflow-prompt", "tenant1", "user1");
        var firstInstanceId = first.InstanceId;

        var newInstance = _engine.GetCurrent("test-workflow-prompt", "tenant1", "user1", action: "start-new");

        newInstance.InstanceId.Should().NotBe(firstInstanceId);
        newInstance.ResponseState.Should().Be("render");
    }

    [Fact]
    public void PromptPolicy_AfterCompletion_NextCallCreatesNewInstance()
    {
        _engine.ResetAll();

        var first = _engine.GetCurrent("test-workflow-prompt", "tenant1", "user1");
        var firstInstanceId = first.InstanceId;

        _engine.Advance(firstInstanceId, "tenant1", "user1", "submit", expectedStateVersion: 0, fieldValues: new Dictionary<string, object?>());

        var nextCall = _engine.GetCurrent("test-workflow-prompt", "tenant1", "user1");

        nextCall.InstanceId.Should().NotBe(firstInstanceId);
        nextCall.ResponseState.Should().Be("render");
    }

    // -----------------------------------------------------------------------
    // Cross-policy: instanceId param takes precedence
    // -----------------------------------------------------------------------

    [Fact]
    public void InstanceIdParam_AlwaysResumesSpecificInstance_SinglePolicy()
    {
        _engine.ResetAll();

        var first = _engine.GetCurrent("test-workflow-single", "tenant1", "user1");
        var firstInstanceId = first.InstanceId;

        var resumed = _engine.GetCurrent("test-workflow-single", "tenant1", "user1", instanceId: firstInstanceId);

        resumed.InstanceId.Should().Be(firstInstanceId);
        resumed.ResponseState.Should().Be("render");
    }

    [Fact]
    public void InstanceIdParam_AlwaysResumesSpecificInstance_MultiplePolicy()
    {
        _engine.ResetAll();

        var first = _engine.GetCurrent("test-workflow-multiple", "tenant1", "user1");
        var firstInstanceId = first.InstanceId;

        var second = _engine.GetCurrent("test-workflow-multiple", "tenant1", "user1");
        second.InstanceId.Should().NotBe(firstInstanceId);

        var resumed = _engine.GetCurrent("test-workflow-multiple", "tenant1", "user1", instanceId: firstInstanceId);

        resumed.InstanceId.Should().Be(firstInstanceId);
        resumed.ResponseState.Should().Be("render");
    }

    [Fact]
    public void InstanceIdParam_AlwaysResumesSpecificInstance_PromptPolicy()
    {
        _engine.ResetAll();

        var first = _engine.GetCurrent("test-workflow-prompt", "tenant1", "user1");
        var firstInstanceId = first.InstanceId;

        var resumed = _engine.GetCurrent("test-workflow-prompt", "tenant1", "user1", instanceId: firstInstanceId);

        resumed.InstanceId.Should().Be(firstInstanceId);
        resumed.ResponseState.Should().Be("render");
    }

    [Fact]
    public void InstanceIdParam_DifferentTenant_ReturnsAccessDenied()
    {
        _engine.ResetAll();

        var instance = _engine.GetCurrent("test-workflow-single", "tenant1", "user1");
        var instanceId = instance.InstanceId;

        var result = _engine.GetCurrent("test-workflow-single", "tenant2", "user1", instanceId: instanceId);

        result.ResponseState.Should().Be("error");
        result.Problems.Should().ContainSingle(p => p.Code == "ACCESS_DENIED");
    }

    [Fact]
    public void InstanceIdParam_DifferentUser_ReturnsAccessDenied()
    {
        _engine.ResetAll();

        var instance = _engine.GetCurrent("test-workflow-single", "tenant1", "user1");
        var instanceId = instance.InstanceId;

        var result = _engine.GetCurrent("test-workflow-single", "tenant1", "user2", instanceId: instanceId);

        result.ResponseState.Should().Be("error");
        result.Problems.Should().ContainSingle(p => p.Code == "ACCESS_DENIED");
    }

    [Fact]
    public void InstanceIdParam_UnknownInstanceId_ReturnsInstanceNotFound()
    {
        _engine.ResetAll();

        var unknownInstanceId = Guid.NewGuid().ToString();
        var result = _engine.GetCurrent("test-workflow-single", "tenant1", "user1", instanceId: unknownInstanceId);

        result.ResponseState.Should().Be("error");
        result.Problems.Should().ContainSingle(p => p.Code == "INSTANCE_NOT_FOUND");
    }
}
