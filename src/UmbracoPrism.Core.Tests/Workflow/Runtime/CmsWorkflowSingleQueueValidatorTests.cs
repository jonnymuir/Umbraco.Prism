using FluentAssertions;
using UmbracoPrism.Core.Services.Workflow;
using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.Core.Tests.Workflow.Runtime;

public class CmsWorkflowSingleQueueValidatorTests
{
    private static readonly CmsWorkflowSingleQueueValidator Validator = new();

    private static WorkflowDefinitionFile BuildWorkflow(params WorkflowQueueDefinition[] queues) => new()
    {
        DefinitionKey = "apply-for-a-juggling-licence",
        DisplayName = "Apply for a juggling licence",
        InitialState = "eligibility",
        Queues = queues
    };

    [Fact]
    public void Validate_TheOneAllowedQueue_ProducesNoDiagnostics()
    {
        var workflow = BuildWorkflow(new WorkflowQueueDefinition { Key = CmsWorkflowQueue.Key, DisplayName = CmsWorkflowQueue.DisplayName });

        Validator.Validate(workflow).Should().BeEmpty();
    }

    [Fact]
    public void Validate_NoQueues_ReportsSingleQueueViolation()
    {
        var workflow = BuildWorkflow();

        var diagnostics = Validator.Validate(workflow).ToList();

        diagnostics.Should().ContainSingle(d => d.Code == "CMS_WORKFLOW_SINGLE_QUEUE_ONLY");
    }

    [Fact]
    public void Validate_WrongQueueKey_ReportsSingleQueueViolation()
    {
        var workflow = BuildWorkflow(new WorkflowQueueDefinition { Key = "web-user", DisplayName = "Web user" });

        var diagnostics = Validator.Validate(workflow).ToList();

        diagnostics.Should().ContainSingle(d => d.Code == "CMS_WORKFLOW_SINGLE_QUEUE_ONLY");
    }

    [Fact]
    public void Validate_MultipleQueues_ReportsSingleQueueViolation()
    {
        var workflow = BuildWorkflow(
            new WorkflowQueueDefinition { Key = CmsWorkflowQueue.Key, DisplayName = CmsWorkflowQueue.DisplayName },
            new WorkflowQueueDefinition { Key = "admin", DisplayName = "Admin" });

        var diagnostics = Validator.Validate(workflow).ToList();

        diagnostics.Should().ContainSingle(d => d.Code == "CMS_WORKFLOW_SINGLE_QUEUE_ONLY");
    }
}
