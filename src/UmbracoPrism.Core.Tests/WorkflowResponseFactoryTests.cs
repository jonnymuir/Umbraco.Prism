using FluentAssertions;
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class WorkflowResponseFactoryTests
{
    private const string TenantA = "tenant-a";
    private const string InstanceId = "wf-instance-123";
    private const string CorrelationId = "correlation-456";
    private const int StateVersion = 5;

    [Fact]
    public void FromInstance_WhenStatusIsActive_AndArchetypeIsCollect_ReturnsAskNow()
    {
        var renderPayload = new WorkflowRenderPayload
        {
            Archetype = "Collect",
            StateDisplayName = "Draft",
            FieldGroups = Array.Empty<FieldGroupRenderPayload>(),
            AvailableActions = Array.Empty<WorkflowAction>()
        };

        var result = WorkflowResponseFactory.AskNow(renderPayload, InstanceId, StateVersion, CorrelationId);

        result.ResponseState.Should().Be("ask_now");
        result.Render.Should().NotBeNull();
        result.Render!.Archetype.Should().Be("Collect");
    }

    [Fact]
    public void FromInstance_WhenStatusIsActive_AndArchetypeIsTaskQueue_ReturnsWait()
    {
        var result = WorkflowResponseFactory.Wait(5000, InstanceId, StateVersion, CorrelationId);

        result.ResponseState.Should().Be("wait");
        result.PollAfterMs.Should().Be(5000);
        result.Render.Should().BeNull();
    }

    [Fact]
    public void FromInstance_WhenStatusIsComplete_ReturnsComplete()
    {
        var result = WorkflowResponseFactory.Complete("approved", InstanceId, StateVersion, CorrelationId);

        result.ResponseState.Should().Be("complete");
        result.Render.Should().NotBeNull();
        result.Render!.Archetype.Should().Be("Completion");
        result.Render!.StateDisplayName.Should().Be("approved");
    }

    [Fact]
    public void FromInstance_WhenStatusIsCancelled_ReturnsError()
    {
        var problems = new List<WorkflowProblem>
        {
            new() { FieldKey = "", Message = "Instance was cancelled", Code = "cancelled" }
        };

        var result = WorkflowResponseFactory.Error(problems, InstanceId, CorrelationId);

        result.ResponseState.Should().Be("error");
        result.Problems.Should().HaveCount(1);
        result.Problems[0].Code.Should().Be("cancelled");
    }

    [Fact]
    public void FromInstance_AlwaysPopulatesStateVersion()
    {
        var renderPayload = new WorkflowRenderPayload
        {
            Archetype = "Collect",
            StateDisplayName = "Draft",
            FieldGroups = Array.Empty<FieldGroupRenderPayload>(),
            AvailableActions = Array.Empty<WorkflowAction>()
        };

        var result = WorkflowResponseFactory.AskNow(renderPayload, InstanceId, StateVersion, CorrelationId);

        result.StateVersion.Should().Be(StateVersion);
    }

    [Fact]
    public void FromInstance_AlwaysPopulatesServerTimeUtc()
    {
        var before = DateTimeOffset.UtcNow;

        var renderPayload = new WorkflowRenderPayload
        {
            Archetype = "Collect",
            StateDisplayName = "Draft",
            FieldGroups = Array.Empty<FieldGroupRenderPayload>(),
            AvailableActions = Array.Empty<WorkflowAction>()
        };

        var result = WorkflowResponseFactory.AskNow(renderPayload, InstanceId, StateVersion, CorrelationId);

        var after = DateTimeOffset.UtcNow;

        result.ServerTimeUtc.Should().BeOnOrAfter(before);
        result.ServerTimeUtc.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void Error_ReturnsErrorResponseState()
    {
        var problems = new List<WorkflowProblem>
        {
            new() { FieldKey = "", Message = "Workflow not found", Code = "not-found" }
        };

        var result = WorkflowResponseFactory.Error(problems, null, CorrelationId);

        result.ResponseState.Should().Be("error");
    }

    [Fact]
    public void Error_IncludesProblems()
    {
        var problems = new List<WorkflowProblem>
        {
            new() { FieldKey = "email", Message = "Email is required", Code = "required" },
            new() { FieldKey = "name", Message = "Name is required", Code = "required" }
        };

        var result = WorkflowResponseFactory.Error(problems, InstanceId, CorrelationId);

        result.Problems.Should().HaveCount(2);
        result.Problems[0].FieldKey.Should().Be("email");
        result.Problems[1].FieldKey.Should().Be("name");
    }

    [Fact]
    public void Wait_ReturnsPollAfterMs()
    {
        var result = WorkflowResponseFactory.Wait(3000, InstanceId, StateVersion, CorrelationId);

        result.PollAfterMs.Should().Be(3000);
    }

    [Fact]
    public void FromInstance_InstanceId_MatchesInput()
    {
        var renderPayload = new WorkflowRenderPayload
        {
            Archetype = "Collect",
            StateDisplayName = "Draft",
            FieldGroups = Array.Empty<FieldGroupRenderPayload>(),
            AvailableActions = Array.Empty<WorkflowAction>()
        };

        var result = WorkflowResponseFactory.AskNow(renderPayload, InstanceId, StateVersion, CorrelationId);

        result.InstanceId.Should().Be(InstanceId);
    }
}
