using System.Text.Json;
using FluentAssertions;
using Moq;
using UmbracoPrism.Shared.Models.ServiceDesign;
using UmbracoPrism.ProcessManager.Abstractions;
using UmbracoPrism.ProcessManager.Services;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Runtime;

/// <summary>
/// End-to-end verification of the "Apply for a juggling licence" CMS Workflow demo definition
/// — the toolkit's own <see cref="ServiceBlueprintSimulationRunner"/> walking the full journey is a far
/// stronger check than manual reasoning about the JSON, and covers exactly the two journeys the
/// demo exists to prove: an anonymous visitor (no membership data resolved) and a logged-in
/// Juggling Society member (membership tier resolved, fee discount applied) through the *same*
/// declarative definition with no special-casing.
/// </summary>
public class JugglingLicenceCmsServiceBlueprintTests
{
    [Fact]
    public void Definition_LoadsAndDeserializesCleanly()
    {
        var definition = LoadDefinition();

        definition.DefinitionKey.Should().Be("apply-for-a-juggling-licence");
        definition.Queues.Should().ContainSingle(q => q.Key == "cms-visitor",
            "a CMS Workflow definition runs on exactly the one well-known queue");
        definition.Stages.Should().HaveCount(5);
    }

    [Fact]
    public void Definition_PassesAuthoringValidation_WithMemberFieldMocked()
    {
        var definition = LoadDefinition();
        var authoringService = new ServiceBlueprintAuthoringService(new Mock<IServiceBlueprintSourceStore>().Object);
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

        var result = new ServiceBlueprintSimulationRunner().Run(definition, BuildWalkthroughSteps(), mockServiceInputs);

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

        var result = new ServiceBlueprintSimulationRunner().Run(definition, BuildWalkthroughSteps(), mockServiceInputs);

        result.Trace[^1].ResponseState.Should().Be("complete");
        result.Calculations[^1]!.Fields["isMember"].Should().Be(true);
        result.Calculations[^1]!.Fields["membershipTier"].Should().Be("Competitive");
        result.Calculations[^1]!.Fields["feeAmount"].Should().Be(20m, "Competitive members receive the discounted fee");
    }

    [Fact]
    public void Simulate_LoggedInMember_LicenceTypeIsPreFilledFromMembershipTier_BeforeAnySubmission()
    {
        var definition = LoadDefinition();
        var mockServiceInputs = new Dictionary<string, object?>
        {
            ["member"] = new Dictionary<string, object?> { ["tier"] = "Professional" }
        };

        // Only the first two steps — stop right before licence-type would be submitted, so this
        // reads the field's suggested value, not a value the walkthrough itself supplied.
        var steps = new[]
        {
            new ProcessManagerSimulationStep("continue", new Dictionary<string, object?>
            {
                ["age-confirmation"] = true,
                ["uk-address-confirmation"] = true
            }),
            new ProcessManagerSimulationStep("continue", new Dictionary<string, object?>
            {
                ["full-name"] = "Alex Juggler",
                ["email-address"] = "alex@example.test",
                ["date-of-birth"] = "12/03/1990"
            })
        };

        var result = new ServiceBlueprintSimulationRunner().Run(definition, steps, mockServiceInputs);

        var licenceTypeField = result.Trace[^1].Render!.Components
            .SelectMany(c => c.Fields)
            .Single(f => f.FieldKey == "licence-type");

        licenceTypeField.Value.Should().Be("Professional",
            "defaultFrom should suggest the member's own tier before they've chosen anything");
    }

    [Fact]
    public void Simulate_LoggedInMember_CanOverrideTheSuggestedLicenceType()
    {
        var definition = LoadDefinition();
        var mockServiceInputs = new Dictionary<string, object?>
        {
            ["member"] = new Dictionary<string, object?> { ["tier"] = "Professional" }
        };

        // Submits "Recreational" despite the member's tier being "Professional" — proves
        // defaultFrom is a genuine, overridable default, not a locked-in value.
        var result = new ServiceBlueprintSimulationRunner().Run(definition, BuildWalkthroughSteps(overrideLicenceType: "Recreational"), mockServiceInputs);

        result.Trace[^1].ResponseState.Should().Be("complete");

        var checkAnswersEnvelope = result.Trace.First(e => e.Render?.StepType == "check-answers");
        var summaryValue = checkAnswersEnvelope.Render!.Components
            .SelectMany(c => c.Fields)
            .Single(f => f.FieldKey == "licence-type")
            .Value;

        summaryValue.Should().Be("Recreational", "the visitor's own submitted choice always wins over the suggested default");
    }

    [Fact]
    public void Simulate_RecreationalMember_DoesNotReceiveTheDiscount()
    {
        var definition = LoadDefinition();
        var mockServiceInputs = new Dictionary<string, object?>
        {
            ["member"] = new Dictionary<string, object?> { ["tier"] = "Recreational" }
        };

        var result = new ServiceBlueprintSimulationRunner().Run(definition, BuildWalkthroughSteps(), mockServiceInputs);

        result.Calculations[^1]!.Fields["isMember"].Should().Be(true);
        result.Calculations[^1]!.Fields["feeAmount"].Should().Be(25m, "the discount is Competitive/Professional-only");
    }

    private static IReadOnlyList<ProcessManagerSimulationStep> BuildWalkthroughSteps(string overrideLicenceType = "Competitive") =>
    [
        new ProcessManagerSimulationStep("continue", new Dictionary<string, object?>
        {
            ["age-confirmation"] = true,
            ["uk-address-confirmation"] = true
        }),
        new ProcessManagerSimulationStep("continue", new Dictionary<string, object?>
        {
            ["full-name"] = "Alex Juggler",
            ["email-address"] = "alex@example.test",
            ["date-of-birth"] = "12/03/1990"
        }),
        new ProcessManagerSimulationStep("continue", new Dictionary<string, object?>
        {
            ["licence-type"] = overrideLicenceType,
            ["declaration"] = true
        }),
        new ProcessManagerSimulationStep("submit")
    ];

    private static ServiceBlueprint LoadDefinition()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src", "UmbracoPrism.TestSite", "cms-service-blueprints", "apply-for-a-juggling-licence.json");
            if (File.Exists(candidate))
            {
                var workflow = JsonSerializer.Deserialize<ServiceBlueprint>(
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
