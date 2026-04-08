using FluentAssertions;
using Moq;
using UmbracoPrism.Core.Exceptions;
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.Core.Services;
using Umbraco.Cms.Infrastructure.Persistence;

namespace UmbracoPrism.Core.Tests;

public class WorkflowStateMachineTests
{
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";
    private const string UserId = "user-123";
    private const string InstanceId = "wf-instance-456";
    private const string WorkflowKey = "information-request";

    [Fact]
    public async Task Advance_WhenTransitionIsValid_TransitionsToNewState()
    {
        var service = new Mock<IWorkflowInstanceService>();
        service.Setup(s => s.AdvanceAsync(TenantA, UserId, InstanceId, "submit", 1, null))
            .ReturnsAsync(new WorkflowResponseEnvelope
            {
                InstanceId = InstanceId,
                ResponseState = "ask_now",
                StateVersion = 2,
                CorrelationId = "corr-123",
                ServerTimeUtc = DateTimeOffset.UtcNow,
                Render = new WorkflowRenderPayload
                {
                    Archetype = "Review",
                    StateDisplayName = "Submitted",
                    FieldGroups = Array.Empty<FieldGroupRenderPayload>(),
                    AvailableActions = Array.Empty<WorkflowAction>()
                },
                Problems = Array.Empty<WorkflowProblem>()
            });

        var result = await service.Object.AdvanceAsync(TenantA, UserId, InstanceId, "submit", 1, null);

        result.StateVersion.Should().Be(2);
        result.Render!.StateDisplayName.Should().Be("Submitted");
    }

    [Fact]
    public async Task Advance_WhenTransitionIsValid_IncrementsStateVersion()
    {
        var service = new Mock<IWorkflowInstanceService>();
        service.Setup(s => s.AdvanceAsync(TenantA, UserId, InstanceId, "approve", 3, null))
            .ReturnsAsync(new WorkflowResponseEnvelope
            {
                InstanceId = InstanceId,
                ResponseState = "ask_now",
                StateVersion = 4,
                CorrelationId = "corr-123",
                ServerTimeUtc = DateTimeOffset.UtcNow,
                Render = new WorkflowRenderPayload
                {
                    Archetype = "Completion",
                    StateDisplayName = "Approved",
                    FieldGroups = Array.Empty<FieldGroupRenderPayload>(),
                    AvailableActions = Array.Empty<WorkflowAction>()
                },
                Problems = Array.Empty<WorkflowProblem>()
            });

        var result = await service.Object.AdvanceAsync(TenantA, UserId, InstanceId, "approve", 3, null);

        result.StateVersion.Should().Be(4);
    }

    [Fact]
    public async Task Advance_WhenComplete_ReturnsCompleteResponseState()
    {
        var service = new Mock<IWorkflowInstanceService>();
        service.Setup(s => s.AdvanceAsync(TenantA, UserId, InstanceId, "finalize", 5, null))
            .ReturnsAsync(new WorkflowResponseEnvelope
            {
                InstanceId = InstanceId,
                ResponseState = "complete",
                StateVersion = 6,
                CorrelationId = "corr-123",
                ServerTimeUtc = DateTimeOffset.UtcNow,
                Render = new WorkflowRenderPayload
                {
                    Archetype = "Completion",
                    StateDisplayName = "Approved",
                    FieldGroups = Array.Empty<FieldGroupRenderPayload>(),
                    AvailableActions = Array.Empty<WorkflowAction>()
                },
                Problems = Array.Empty<WorkflowProblem>()
            });

        var result = await service.Object.AdvanceAsync(TenantA, UserId, InstanceId, "finalize", 5, null);

        result.ResponseState.Should().Be("complete");
    }

    [Fact]
    public async Task Advance_WhenStateVersionMismatch_ThrowsOptimisticConcurrencyException()
    {
        var service = new Mock<IWorkflowInstanceService>();
        service.Setup(s => s.AdvanceAsync(TenantA, UserId, InstanceId, "submit", 2, null))
            .ThrowsAsync(new OptimisticConcurrencyException(InstanceId, 2, 5));

        var act = async () => await service.Object.AdvanceAsync(TenantA, UserId, InstanceId, "submit", 2, null);

        await act.Should().ThrowAsync<OptimisticConcurrencyException>()
            .WithMessage("*version*");
    }

    [Fact]
    public async Task Advance_WhenActionNotValidInCurrentState_ThrowsInvalidWorkflowTransitionException()
    {
        var service = new Mock<IWorkflowInstanceService>();
        service.Setup(s => s.AdvanceAsync(TenantA, UserId, InstanceId, "approve", 1, null))
            .ThrowsAsync(new InvalidWorkflowTransitionException("draft", "approve"));

        var act = async () => await service.Object.AdvanceAsync(TenantA, UserId, InstanceId, "approve", 1, null);

        await act.Should().ThrowAsync<InvalidWorkflowTransitionException>();
    }

    [Fact]
    public async Task Advance_WhenInstanceNotFound_ThrowsWorkflowInstanceNotFoundException()
    {
        var service = new Mock<IWorkflowInstanceService>();
        service.Setup(s => s.AdvanceAsync(TenantA, UserId, "non-existent", "submit", 1, null))
            .ThrowsAsync(new WorkflowInstanceNotFoundException("non-existent"));

        var act = async () => await service.Object.AdvanceAsync(TenantA, UserId, "non-existent", "submit", 1, null);

        await act.Should().ThrowAsync<WorkflowInstanceNotFoundException>();
    }

    [Fact]
    public async Task GetCurrentState_WhenInstanceBelongsToDifferentTenant_ThrowsUnauthorizedWorkflowAccessException()
    {
        var service = new Mock<IWorkflowInstanceService>();
        service.Setup(s => s.GetCurrentStateAsync(TenantA, UserId, InstanceId))
            .ThrowsAsync(new UnauthorizedWorkflowAccessException(InstanceId));

        var act = async () => await service.Object.GetCurrentStateAsync(TenantA, UserId, InstanceId);

        await act.Should().ThrowAsync<UnauthorizedWorkflowAccessException>();
    }

    [Fact]
    public async Task Advance_WhenInstanceBelongsToDifferentTenant_ThrowsUnauthorizedWorkflowAccessException()
    {
        var service = new Mock<IWorkflowInstanceService>();
        service.Setup(s => s.AdvanceAsync(TenantA, UserId, InstanceId, "submit", 1, null))
            .ThrowsAsync(new UnauthorizedWorkflowAccessException(InstanceId));

        var act = async () => await service.Object.AdvanceAsync(TenantA, UserId, InstanceId, "submit", 1, null);

        await act.Should().ThrowAsync<UnauthorizedWorkflowAccessException>();
    }

    [Fact]
    public async Task Create_WhenDefinitionExists_ReturnsAskNowEnvelope()
    {
        var service = new Mock<IWorkflowInstanceService>();
        service.Setup(s => s.CreateAsync(TenantA, UserId, WorkflowKey, null))
            .ReturnsAsync(new WorkflowResponseEnvelope
            {
                InstanceId = InstanceId,
                ResponseState = "ask_now",
                StateVersion = 1,
                CorrelationId = "corr-123",
                ServerTimeUtc = DateTimeOffset.UtcNow,
                Render = new WorkflowRenderPayload
                {
                    Archetype = "Collect",
                    StateDisplayName = "Draft",
                    FieldGroups = Array.Empty<FieldGroupRenderPayload>(),
                    AvailableActions = Array.Empty<WorkflowAction>()
                },
                Problems = Array.Empty<WorkflowProblem>()
            });

        var result = await service.Object.CreateAsync(TenantA, UserId, WorkflowKey, null);

        result.InstanceId.Should().NotBeNullOrEmpty();
        result.ResponseState.Should().Be("ask_now");
        result.StateVersion.Should().Be(1);
    }

    [Fact]
    public async Task Create_WhenDefinitionNotFound_ThrowsException()
    {
        var service = new Mock<IWorkflowInstanceService>();
        service.Setup(s => s.CreateAsync(TenantA, UserId, "non-existent-workflow", null))
            .ThrowsAsync(new InvalidOperationException("Workflow definition 'non-existent-workflow' not found"));

        var act = async () => await service.Object.CreateAsync(TenantA, UserId, "non-existent-workflow", null);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task Create_SetsInitialStateFromDefinition()
    {
        var service = new Mock<IWorkflowInstanceService>();
        service.Setup(s => s.CreateAsync(TenantA, UserId, WorkflowKey, null))
            .ReturnsAsync(new WorkflowResponseEnvelope
            {
                InstanceId = InstanceId,
                ResponseState = "ask_now",
                StateVersion = 1,
                CorrelationId = "corr-123",
                ServerTimeUtc = DateTimeOffset.UtcNow,
                Render = new WorkflowRenderPayload
                {
                    Archetype = "Collect",
                    StateDisplayName = "Draft",
                    FieldGroups = Array.Empty<FieldGroupRenderPayload>(),
                    AvailableActions = Array.Empty<WorkflowAction>()
                },
                Problems = Array.Empty<WorkflowProblem>()
            });

        var result = await service.Object.CreateAsync(TenantA, UserId, WorkflowKey, null);

        result.Render!.StateDisplayName.Should().Be("Draft");
    }

    [Fact]
    public async Task Create_SetsStateVersionToOne()
    {
        var service = new Mock<IWorkflowInstanceService>();
        service.Setup(s => s.CreateAsync(TenantA, UserId, WorkflowKey, null))
            .ReturnsAsync(new WorkflowResponseEnvelope
            {
                InstanceId = InstanceId,
                ResponseState = "ask_now",
                StateVersion = 1,
                CorrelationId = "corr-123",
                ServerTimeUtc = DateTimeOffset.UtcNow,
                Render = new WorkflowRenderPayload
                {
                    Archetype = "Collect",
                    StateDisplayName = "Draft",
                    FieldGroups = Array.Empty<FieldGroupRenderPayload>(),
                    AvailableActions = Array.Empty<WorkflowAction>()
                },
                Problems = Array.Empty<WorkflowProblem>()
            });

        var result = await service.Object.CreateAsync(TenantA, UserId, WorkflowKey, null);

        result.StateVersion.Should().Be(1);
    }

    [Fact]
    public async Task Cancel_WhenActive_TransitionsToCancelledStatus()
    {
        var service = new Mock<IWorkflowInstanceService>();
        service.Setup(s => s.CancelAsync(TenantA, UserId, InstanceId))
            .ReturnsAsync(new WorkflowResponseEnvelope
            {
                InstanceId = InstanceId,
                ResponseState = "complete",
                StateVersion = 5,
                CorrelationId = "corr-123",
                ServerTimeUtc = DateTimeOffset.UtcNow,
                Render = new WorkflowRenderPayload
                {
                    Archetype = "Completion",
                    StateDisplayName = "Cancelled",
                    FieldGroups = Array.Empty<FieldGroupRenderPayload>(),
                    AvailableActions = Array.Empty<WorkflowAction>()
                },
                Problems = Array.Empty<WorkflowProblem>()
            });

        var result = await service.Object.CancelAsync(TenantA, UserId, InstanceId);

        result.ResponseState.Should().Be("complete");
        result.Render!.StateDisplayName.Should().Be("Cancelled");
    }

    [Fact]
    public async Task Cancel_WhenAlreadyComplete_ThrowsInvalidWorkflowTransitionException()
    {
        var service = new Mock<IWorkflowInstanceService>();
        service.Setup(s => s.CancelAsync(TenantA, UserId, InstanceId))
            .ThrowsAsync(new InvalidWorkflowTransitionException("complete", "cancel"));

        var act = async () => await service.Object.CancelAsync(TenantA, UserId, InstanceId);

        await act.Should().ThrowAsync<InvalidWorkflowTransitionException>();
    }
}
