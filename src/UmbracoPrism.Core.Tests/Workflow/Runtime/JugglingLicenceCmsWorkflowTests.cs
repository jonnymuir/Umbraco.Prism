using System.Text.Json;
using FluentAssertions;
using Moq;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.WorkflowRuntime.Abstractions;
using UmbracoPrism.WorkflowRuntime.Services;

namespace UmbracoPrism.Core.Tests.Workflow.Runtime;

/// <summary>
/// End-to-end verification of the "Apply for a juggling licence" CMS Workflow demo definition
/// — the toolkit's own <see cref="WorkflowSimulationRunner"/> walking the full journey is a far
/// stronger check than manual reasoning about the JSON, and covers exactly the two journeys the
/// demo exists to prove: an anonymous visitor (no membership data resolved) and a logged-in
/// Juggling Society member (membership tier resolved, fee discount applied) through the *same*
/// declarative definition with no special-casing.
/// </summary>
public class JugglingLicenceCmsWorkflowTests
{
    [Fact]
    public void Definition_LoadsAndDeserializesCleanly()
    {
        var definition = LoadDefinition();

        definition.DefinitionKey.Should().Be("apply-for-a-juggling-licence");
        definition.Queues.Should().ContainSingle(q => q.Key == "cms-visitor",
            "a CMS Workflow definition runs on exactly the one well-known queue");
        definition.States.Should().HaveCount(5);
    }

    [Fact]
    public void Definition_PassesAuthoringValidation_WithMemberFieldMocked()
    {
        var definition = LoadDefinition();
        var authoringService = new WorkflowAuthoringService(new Mock<IWorkflowSourceStore>().Object);
        var mockServiceInputs = new Dictionary<string, object?>
        {
            ["member"] = new Dictionary<string, object?> { ["tier"] = "Competitive" }
        };

        var outcome = authoringService.Validate(definition, mockServiceInputs);

        outcome.IsValid.Should().BeTrue(
            outcome.Diagnostics.Count > 0
                ? string.Join("; ", outcome.Diagnostics.Select(d => $"{d.Code} {d.Path}: {d.Message}"))
                : "expected no diagnostics");
    }

    [Fact]
    public void Simulate_AnonymousVisitor_ReachesConfirmation_WithUndiscountedFee()
    {
        var definition = LoadDefinition();
        var mockServiceInputs = new Dictionary<string, object?>
        {
            ["member"] = new Dictionary<string, object?> { ["tier"] = "" }
        };

        var result = new WorkflowSimulationRunner().Run(definition, BuildWalkthroughSteps(), mockServiceInputs);

        result.Trace.Should().HaveCount(5, "initial GetCurrent plus four Advance steps to confirmation");
        result.Trace[^1].ResponseState.Should().Be("complete");
        result.Calculations.Should().OnlyContain(c => c != null,
            "member is always resolved (with an empty tier sentinel for non-members), so calculations never fail");
        result.Calculations[^1]!.Fields["feeAmount"].Should().Be(25m, "no membership discount applies");
        result.Calculations[^1]!.Fields["isMember"].Should().Be(false);
    }

    [Fact]
    public void Simulate_LoggedInCompetitiveMember_ReachesConfirmation_WithDiscountedFee()
    {
        var definition = LoadDefinition();
        var mockServiceInputs = new Dictionary<string, object?>
        {
            ["member"] = new Dictionary<string, object?> { ["tier"] = "Competitive" }
        };

        var result = new WorkflowSimulationRunner().Run(definition, BuildWalkthroughSteps(), mockServiceInputs);

        result.Trace[^1].ResponseState.Should().Be("complete");
        result.Calculations[^1]!.Fields["isMember"].Should().Be(true);
        result.Calculations[^1]!.Fields["membershipTier"].Should().Be("Competitive");
        result.Calculations[^1]!.Fields["feeAmount"].Should().Be(20m, "Competitive members receive the discounted fee");
    }

    [Fact]
    public void Simulate_RecreationalMember_DoesNotReceiveTheDiscount()
    {
        var definition = LoadDefinition();
        var mockServiceInputs = new Dictionary<string, object?>
        {
            ["member"] = new Dictionary<string, object?> { ["tier"] = "Recreational" }
        };

        var result = new WorkflowSimulationRunner().Run(definition, BuildWalkthroughSteps(), mockServiceInputs);

        result.Calculations[^1]!.Fields["isMember"].Should().Be(true);
        result.Calculations[^1]!.Fields["feeAmount"].Should().Be(25m, "the discount is Competitive/Professional-only");
    }

    private static IReadOnlyList<WorkflowRuntimeSimulationStep> BuildWalkthroughSteps() =>
    [
        new WorkflowRuntimeSimulationStep("continue", new Dictionary<string, object?>
        {
            ["age-confirmation"] = true,
            ["uk-address-confirmation"] = true
        }),
        new WorkflowRuntimeSimulationStep("continue", new Dictionary<string, object?>
        {
            ["full-name"] = "Alex Juggler",
            ["email-address"] = "alex@example.test",
            ["date-of-birth"] = "12/03/1990"
        }),
        new WorkflowRuntimeSimulationStep("continue", new Dictionary<string, object?>
        {
            ["licence-type"] = "Competitive",
            ["declaration"] = true
        }),
        new WorkflowRuntimeSimulationStep("submit")
    ];

    private static WorkflowDefinitionFile LoadDefinition()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src", "UmbracoPrism.TestSite", "cms-workflow-seeds", "apply-for-a-juggling-licence.json");
            if (File.Exists(candidate))
            {
                var workflow = JsonSerializer.Deserialize<WorkflowDefinitionFile>(
                    File.ReadAllText(candidate),
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        PropertyNameCaseInsensitive = true,
                        AllowOutOfOrderMetadataProperties = true
                    });
                return workflow ?? throw new InvalidOperationException("Deserialized to null.");
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate apply-for-a-juggling-licence.json by walking up from the test working directory.");
    }
}
