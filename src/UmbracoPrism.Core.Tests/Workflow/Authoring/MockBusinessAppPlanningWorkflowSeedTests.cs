extern alias MockBusinessApp;

using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.Shared.Services.Sanitization;
using BusinessAppWorkflowEngine = MockBusinessApp::UmbracoPrism.MockBusinessApp.Services.BusinessAppWorkflowEngine;
using ReferenceWorkflowDefinitionStore = MockBusinessApp::UmbracoPrism.MockBusinessApp.Services.ReferenceWorkflowDefinitionStore;
using MockReferenceWorkflowRepository = MockBusinessApp::UmbracoPrism.MockBusinessApp.Services.ReferenceWorkflowRepository;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

/// <summary>
/// Validates that MockBusinessApp reference workflow seeds use the flattened
/// workflow-definition contract and stay aligned with the four-workflow reference set.
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
        planningWorkflow!.DefinitionKey.Should().Be("planning");
        planningWorkflow.States.Should().NotBeEmpty();
        planningWorkflow.Transitions.Should().NotBeEmpty();
    }

    [Fact]
    public void ReferenceWorkflows_WithTraceIds_HaveUniqueIds()
    {
        var referenceWorkflows = MockReferenceWorkflowRepository.GetReferenceWorkflows();

        var workflowIds = referenceWorkflows
            .Select(kvp => kvp.Value.AuthoredWorkflowId)
            .Where(id => id is not null)
            .ToList();
        var distinctIds = workflowIds.Distinct().ToList();

        workflowIds.Should().HaveCount(distinctIds.Count,
            because: "any preserved trace ids should remain unique");
    }

    [Fact]
    public void PlanningWorkflow_RemainsReachableByHostKeyAtRuntime()
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.ContentRootPath).Returns(AppContext.BaseDirectory);

        var sanitizer = new Mock<IWorkflowContentSanitizer>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string?>())).Returns<string?>(value => value ?? string.Empty);

        var engine = new BusinessAppWorkflowEngine(
            NullLogger<BusinessAppWorkflowEngine>.Instance,
            environment.Object,
            sanitizer.Object,
            new ReferenceWorkflowDefinitionStore());

        var current = engine.GetCurrent("planning", "tenant", "user");

        current.ResponseState.Should().Be("render",
            because: "runtime lookups must use the host workflow key even when the authored definition key differs");
        current.Render.Should().NotBeNull();
        current.Render!.StateDisplayName.Should().Be("Declaration");
    }
}
