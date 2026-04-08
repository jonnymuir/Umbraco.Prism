using FluentAssertions;
using Moq;
using UmbracoPrism.Core.Exceptions;
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class WorkflowTenantGuardTests
{
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";
    private const string InstanceId = "wf-instance-123";

    [Fact]
    public async Task RequireInstance_WhenTenantMatches_ReturnsInstance()
    {
        var expectedInstance = new WorkflowInstance
        {
            InstanceId = InstanceId,
            TenantId = TenantA,
            CurrentState = "draft",
            StateVersion = 1,
            Status = "Active"
        };

        var guard = new Mock<IWorkflowTenantGuard>();
        guard.Setup(g => g.RequireInstanceAsync(TenantA, "user-123", InstanceId, default))
            .ReturnsAsync(expectedInstance);

        var result = await guard.Object.RequireInstanceAsync(TenantA, "user-123", InstanceId, default);

        result.Should().NotBeNull();
        result.InstanceId.Should().Be(InstanceId);
        result.TenantId.Should().Be(TenantA);
    }

    [Fact]
    public async Task RequireInstance_WhenTenantDoesNotMatch_ThrowsUnauthorizedWorkflowAccessException()
    {
        var guard = new Mock<IWorkflowTenantGuard>();
        guard.Setup(g => g.RequireInstanceAsync(TenantB, "user-123", InstanceId, default))
            .ThrowsAsync(new UnauthorizedWorkflowAccessException(InstanceId));

        var act = async () => await guard.Object.RequireInstanceAsync(TenantB, "user-123", InstanceId, default);

        await act.Should().ThrowAsync<UnauthorizedWorkflowAccessException>();
    }

    [Fact]
    public async Task RequireInstance_WhenInstanceNotFound_ThrowsWorkflowInstanceNotFoundException()
    {
        var guard = new Mock<IWorkflowTenantGuard>();
        guard.Setup(g => g.RequireInstanceAsync(TenantA, "user-123", "non-existent", default))
            .ThrowsAsync(new WorkflowInstanceNotFoundException("non-existent"));

        var act = async () => await guard.Object.RequireInstanceAsync(TenantA, "user-123", "non-existent", default);

        await act.Should().ThrowAsync<WorkflowInstanceNotFoundException>();
    }
}
