using FluentAssertions;
using UmbracoPrism.Core.Services.ServiceDesign;
using UmbracoPrism.Shared.Models.ServiceDesign;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Runtime;

public class CmsSingleQueueValidatorTests
{
    private static readonly CmsSingleQueueValidator Validator = new();

    private static ServiceBlueprint BuildWorkflow(params QueueDefinition[] queues) => new()
    {
        DefinitionKey = "apply-for-a-juggling-licence",
        DisplayName = "Apply for a juggling licence",
        InitialStage = "eligibility",
        Queues = queues
    };

    [Fact]
    public void Validate_TheOneAllowedQueue_ProducesNoDiagnostics()
    {
        var workflow = BuildWorkflow(new QueueDefinition { Key = CmsQueue.Key, DisplayName = CmsQueue.DisplayName });

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
        var workflow = BuildWorkflow(new QueueDefinition { Key = "web-user", DisplayName = "Web user" });

        var diagnostics = Validator.Validate(workflow).ToList();

        diagnostics.Should().ContainSingle(d => d.Code == "CMS_WORKFLOW_SINGLE_QUEUE_ONLY");
    }

    [Fact]
    public void Validate_MultipleQueues_ReportsSingleQueueViolation()
    {
        var workflow = BuildWorkflow(
            new QueueDefinition { Key = CmsQueue.Key, DisplayName = CmsQueue.DisplayName },
            new QueueDefinition { Key = "admin", DisplayName = "Admin" });

        var diagnostics = Validator.Validate(workflow).ToList();

        diagnostics.Should().ContainSingle(d => d.Code == "CMS_WORKFLOW_SINGLE_QUEUE_ONLY");
    }
}
