extern alias MockBusinessApp;

using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UmbracoPrism.Shared.Models.ServiceDesign;
using UmbracoPrism.Shared.Services.Sanitization;
using BusinessAppProcessManager = MockBusinessApp::UmbracoPrism.MockBusinessApp.Services.BusinessAppProcessManager;
using ReferenceServiceBlueprintStore = MockBusinessApp::UmbracoPrism.MockBusinessApp.Services.ReferenceServiceBlueprintStore;
using MockReferenceWorkflowRepository = MockBusinessApp::UmbracoPrism.MockBusinessApp.Services.ReferenceServiceBlueprintRepository;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Authoring;

/// <summary>
/// Validates that MockBusinessApp reference workflow seeds use the flattened
/// workflow-definition contract and stay aligned with the canonical reference set.
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
        "money-modeller",
        "payment-demo",
        "planning"
    ];

    [Fact]
    public void ReferenceWorkflowRepository_ContainsExactlyTheCanonicalWorkflows()
    {
        var referenceWorkflows = MockReferenceWorkflowRepository.GetReferenceWorkflows();

        referenceWorkflows.Should().HaveCount(5,
            because: "the reference contract specifies exactly 5 demo workflows with authored sources");

        var workflowKeys = referenceWorkflows
            .Select(kvp => kvp.Key)
            .OrderBy(k => k)
            .ToList();

        workflowKeys.Should().BeEquivalentTo(ExpectedAuthoredWorkflows.OrderBy(k => k),
            because: "the reference repository must contain exactly the canonical workflows");
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
        planningWorkflow.Touchpoints.Should().NotBeEmpty();
        planningWorkflow.Queues.Should().NotBeNullOrEmpty(
            because: "planning workflow v3 uses the queues/routes format — top-level Transitions are no longer used");
        planningWorkflow.Touchpoints.Should().AllSatisfy(s =>
            s.Routes.Should().NotBeNull(
                because: $"every state in the new format must have a Routes list (state: '{s.TouchpointKey}')"));
    }

    [Fact]
    public void ReferenceWorkflows_WithTraceIds_HaveUniqueIds()
    {
        var referenceWorkflows = MockReferenceWorkflowRepository.GetReferenceWorkflows();

        var workflowIds = referenceWorkflows
            .Select(kvp => kvp.Value.AuthoredServiceBlueprintId)
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

        var sanitizer = new Mock<IServiceContentSanitizer>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string?>())).Returns<string?>(value => value ?? string.Empty);

        var engine = new BusinessAppProcessManager(
            NullLogger<BusinessAppProcessManager>.Instance,
            environment.Object,
            sanitizer.Object,
            new ReferenceServiceBlueprintStore());

        var current = engine.GetCurrent("planning", "tenant", "user");

        current.ResponseState.Should().Be("render",
            because: "runtime lookups must use the host workflow key even when the authored definition key differs");
        current.Render.Should().NotBeNull();
        current.Render!.StateDisplayName.Should().Be("Declaration");
    }
}
