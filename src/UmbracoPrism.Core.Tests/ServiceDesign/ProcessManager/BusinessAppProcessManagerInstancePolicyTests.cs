using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using UmbracoPrism.Core.Models.ServiceDesign;
using UmbracoPrism.MockBusinessApp.Services;
using UmbracoPrism.Shared.Models.ServiceDesign;
using UmbracoPrism.Shared.Models.ServiceDesign.Components;
using UmbracoPrism.Shared.Services.Sanitization;

namespace UmbracoPrism.Core.Tests.ServiceDesign.ProcessManager;

public class BusinessAppWorkflowEngineInstancePolicyTests : IDisposable
{
    private readonly string _testSeedDir;
    private readonly BusinessAppProcessManager _engine;
    private readonly Mock<IWebHostEnvironment> _mockEnv;

    public BusinessAppWorkflowEngineInstancePolicyTests()
    {
        _testSeedDir = Path.Combine(Directory.GetCurrentDirectory(), $"test-seeds-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testSeedDir);
        Directory.CreateDirectory(Path.Combine(_testSeedDir, "service-blueprints"));

        SeedTestWorkflows();

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

    private void SeedTestWorkflows()
    {
        var seedsDir = Path.Combine(_testSeedDir, "service-blueprints");

        var singlePolicyWorkflow = new ServiceBlueprint
        {
            DefinitionKey = "test-workflow-single",
            DisplayName = "Test Workflow Single",
            Version = 1,
            InitialTouchpoint = "step-1",
            RequestPolicy = "single",
            Touchpoints = new[]
            {
                new StepDefinition
                {
                    TouchpointKey = "step-1",
                    DisplayName = "Step 1",
                    Components = Array.Empty<PrismComponent>()
                },
                new StepDefinition
                {
                    TouchpointKey = "done",
                    DisplayName = "Done",
                    Components = new PrismComponent[] { new PanelComponent { Heading = "Complete" } }
                }
            },
            Transitions = new[]
            {
                new RouteFile
                {
                    FromState = "step-1",
                    ToState = "done",
                    Action = "submit",
                    RequiresRole = null
                }
            }
        };

        var multiplePolicyWorkflow = new ServiceBlueprint
        {
            DefinitionKey = "test-workflow-multiple",
            DisplayName = "Test Workflow Multiple",
            Version = 1,
            InitialTouchpoint = "step-1",
            RequestPolicy = "multiple",
            Touchpoints = new[]
            {
                new StepDefinition
                {
                    TouchpointKey = "step-1",
                    DisplayName = "Step 1",
                    Components = Array.Empty<PrismComponent>()
                },
                new StepDefinition
                {
                    TouchpointKey = "done",
                    DisplayName = "Done",
                    Components = new PrismComponent[] { new PanelComponent { Heading = "Complete" } }
                }
            },
            Transitions = new[]
            {
                new RouteFile
                {
                    FromState = "step-1",
                    ToState = "done",
                    Action = "submit",
                    RequiresRole = null
                }
            }
        };

        var promptPolicyWorkflow = new ServiceBlueprint
        {
            DefinitionKey = "test-workflow-prompt",
            DisplayName = "Test Workflow Prompt",
            Version = 1,
            InitialTouchpoint = "step-1",
            RequestPolicy = "prompt",
            Touchpoints = new[]
            {
                new StepDefinition
                {
                    TouchpointKey = "step-1",
                    DisplayName = "Step 1",
                    Components = Array.Empty<PrismComponent>()
                },
                new StepDefinition
                {
                    TouchpointKey = "done",
                    DisplayName = "Done",
                    Components = new PrismComponent[] { new PanelComponent { Heading = "Complete" } }
                }
            },
            Transitions = new[]
            {
                new RouteFile
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
        result.RequestPolicy.Should().Be("single");
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

    [Fact]
    public void SinglePolicy_ImmediatelyAfterCompletion_FirstGetCurrentStillShowsConfirmation()
    {
        // Mirrors the PRG pattern: PrismWorkflowPageController redirects to a bare GET (no
        // instanceId) after every POST, so THIS call is how the visitor actually sees the
        // confirmation page they just submitted — it must not be silently swapped for a
        // brand-new, blank instance.
        _engine.ResetAll();

        var first = _engine.GetCurrent("test-workflow-single", "tenant1", "user1");
        _engine.Advance(first.InstanceId, "tenant1", "user1", "submit", expectedStateVersion: 0, fieldValues: new Dictionary<string, object?>());

        var confirmation = _engine.GetCurrent("test-workflow-single", "tenant1", "user1");

        confirmation.InstanceId.Should().Be(first.InstanceId);
        confirmation.Render!.StateDisplayName.Should().Be("Done");
    }

    [Fact]
    public void SinglePolicy_LaterVisitAfterCompletion_KeepsShowingTheSameConfirmation()
    {
        // "single" means at most one instance per user for this workflow, full stop — reaching a
        // terminal state doesn't make it any less "the" instance. A member returning to the page
        // later must keep seeing their completed confirmation, not have it silently swapped for a
        // fresh, blank instance (community-enquiry's walkthrough depends on exactly this).
        _engine.ResetAll();

        var first = _engine.GetCurrent("test-workflow-single", "tenant1", "user1");
        _engine.Advance(first.InstanceId, "tenant1", "user1", "submit", expectedStateVersion: 0, fieldValues: new Dictionary<string, object?>());
        var confirmation = _engine.GetCurrent("test-workflow-single", "tenant1", "user1");

        // A later, separate visit — the confirmation has already been shown once.
        var nextVisit = _engine.GetCurrent("test-workflow-single", "tenant1", "user1");

        nextVisit.InstanceId.Should().Be(confirmation.InstanceId);
        nextVisit.Render!.StateDisplayName.Should().Be("Done");
    }

    [Fact]
    public void SinglePolicy_StartNewActionAfterCompletion_CreatesAGenuinelyFreshInstance()
    {
        // The explicit, visitor-initiated escape hatch from the "keep showing the same
        // confirmation" behaviour above: a real "Start again" link (?action=start-new) must
        // still create a brand-new instance even though the existing one is terminal under
        // "single" policy — this is deliberate, opt-in, and distinct from a plain revisit.
        _engine.ResetAll();

        var first = _engine.GetCurrent("test-workflow-single", "tenant1", "user1");
        _engine.Advance(first.InstanceId, "tenant1", "user1", "submit", expectedStateVersion: 0, fieldValues: new Dictionary<string, object?>());
        var confirmation = _engine.GetCurrent("test-workflow-single", "tenant1", "user1");

        var restarted = _engine.GetCurrent("test-workflow-single", "tenant1", "user1", action: "start-new");

        restarted.InstanceId.Should().NotBe(confirmation.InstanceId);
        restarted.ResponseState.Should().Be("render");
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
        result.RequestPolicy.Should().Be("multiple");
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
        result.RequestPolicy.Should().Be("prompt");
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
