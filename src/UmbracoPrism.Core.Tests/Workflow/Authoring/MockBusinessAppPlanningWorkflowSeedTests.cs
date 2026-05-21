extern alias MockBusinessApp;

using System.Text.Json;
using FluentAssertions;
using UmbracoPrism.WorkflowEditor.Authoring;
using MockReferenceWorkflowRepository = MockBusinessApp::UmbracoPrism.MockBusinessApp.Services.ReferenceWorkflowRepository;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

/// <summary>
/// Validates that authored workflow seeds in MockBusinessApp align with the
/// four-workflow reference contract and maintain the authored → published traceability.
/// </summary>
public class MockBusinessAppPlanningWorkflowSeedTests
{
    private static readonly JsonSerializerOptions RoundTripOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static readonly string[] ExpectedAuthoredWorkflows =
    [
        "community-enquiry",
        "information-request",
        "payment-demo",
        "planning"
    ];

    [Fact]
    public void ReferenceWorkflowRepository_ContainsExactlyFourWorkflows()
    {
        var referenceWorkflows = MockReferenceWorkflowRepository.GetReferenceWorkflows();

        referenceWorkflows.Should().HaveCount(4,
            because: "the reference contract specifies exactly 4 demo workflows with authored sources");

        var workflowKeys = referenceWorkflows
            .Select(kvp => kvp.Key)
            .OrderBy(k => k)
            .ToList();

        workflowKeys.Should().BeEquivalentTo(ExpectedAuthoredWorkflows.OrderBy(k => k),
            because: "the reference repository must contain exactly the 4 canonical workflows");
    }

    [Fact]
    public void PlanningSeed_IsDefinedInReferenceRepository()
    {
        var referenceWorkflows = MockReferenceWorkflowRepository.GetReferenceWorkflows();
        var planningWorkflow = referenceWorkflows.FirstOrDefault(kvp => kvp.Key == "planning");

        planningWorkflow.Should().NotBeNull(
            because: "planning workflow is one of the 4 canonical workflows");
        planningWorkflow.Value.Should().NotBeNull();
    }

    [Fact]
    public void PlanningSeed_HasExpectedStructure()
    {
        var referenceWorkflows = MockReferenceWorkflowRepository.GetReferenceWorkflows();
        var planningWorkflow = referenceWorkflows.FirstOrDefault(kvp => kvp.Key == "planning").Value;

        planningWorkflow.Should().NotBeNull();
        planningWorkflow!.DefinitionKey.Should().Be("planning-application");
        planningWorkflow.Stages.Should().NotBeEmpty();
        planningWorkflow.Transitions.Should().NotBeEmpty();
    }

    [Fact]
    public void AllReferenceWorkflows_HaveUniqueIds()
    {
        var referenceWorkflows = MockReferenceWorkflowRepository.GetReferenceWorkflows();
        
        var workflowIds = referenceWorkflows.Select(kvp => kvp.Value.Id).ToList();
        var distinctIds = workflowIds.Distinct().ToList();

        workflowIds.Should().HaveCount(distinctIds.Count,
            because: "each workflow must have a unique ID");
    }
}
