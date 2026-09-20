using System.Text.Json;
using FluentAssertions;
using Moq;
using Wayfinder.Models.ServiceDesign;
using Wayfinder.Models.ServiceDesign.Components;
using Wayfinder.Models.ServiceDesign.SupportSystems;
using Wayfinder.Engine.Abstractions;
using Wayfinder.Engine.Services;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Runtime;

/// <summary>
/// End-to-end verification of the "Apply for a juggling licence" CMS Workflow demo definition
/// — the toolkit's own <see cref="ServiceBlueprintSimulationRunner"/> walking the full journey is a far
/// stronger check than manual reasoning about the JSON, and covers exactly the two journeys the
/// demo exists to prove: an anonymous visitor (no membership data resolved) and a logged-in
/// Juggling Society member (membership tier resolved, fee discount applied) through the *same*
/// declarative definition with no special-casing.
/// <para/>
/// Submitting the application now leaps into the juggling-licence-decision support system (see
/// TestSite's own appsettings.json Wayfinder:SupportSystems and
/// JugglingLicenceDecisionAutomationSeeder) rather than completing immediately — a real host
/// resolves that via a real Umbraco Automate callback, which nothing in this process-isolated
/// unit test can simulate, so every walkthrough here now ends mid-flight, not "complete".
/// <see cref="ServiceBlueprintSimulationRunner"/> calls the engine's own unscoped
/// Advance/GetCurrent overload (no ActorProfile), so its trace renders the automation queue's own
/// "processing-application" stage rather than the public-visitor queue's "application-decided"
/// wait screen a real, ActorProfile-scoped citizen would see — a simulation-harness quirk, not a
/// claim about what an actual visitor sees (that's a live/E2E concern, not this unit test's job).
/// This static constructor registers the same descriptor TestSite's own Wayfinder:SupportSystems
/// config produces (via AddConfiguredSupportSystems), so ValidateSupportSystemActions() and the
/// simulation runner's own engine both resolve the reference correctly — SupportSystemRegistry
/// freezes on first read, so this must run before any test method in this class does, which a
/// static constructor guarantees.
/// </summary>
public class JugglingLicenceCmsServiceBlueprintTests
{
    static JugglingLicenceCmsServiceBlueprintTests()
    {
        try
        {
            SupportSystemRegistry.Register(new SupportSystemDescriptor
            {
                Key = "juggling-licence-decision",
                DisplayName = "Juggling Licence Decision",
                Capabilities =
                [
                    new SupportSystemCapabilityDescriptor
                    {
                        Key = "decide-application",
                        DisplayName = "Decide a juggling licence application",
                        Inputs = [new() { Key = "licenceType", Title = "Licence type", ValueKind = ComponentPropertyValueKind.String, Format = "field-ref", Required = true }],
                        Outputs = [new() { Key = "applicationDecisionNote", Title = "Decision note", ValueKind = ComponentPropertyValueKind.String }],
                        SupportedCompletionModes = [SupportSystemCompletionMode.Webhook],
                        Outcomes = [new() { Key = "approved", DisplayName = "Approved" }, new() { Key = "referred", DisplayName = "Referred" }],
                    },
                ],
            });
        }
        catch (InvalidOperationException)
        {
            // Already registered (e.g. re-run in the same process) — harmless.
        }
    }

    [Fact]
    public void Definition_LoadsAndDeserializesCleanly()
    {
        var definition = LoadDefinition();

        definition.DefinitionKey.Should().Be("apply-for-a-juggling-licence");
        definition.Queues.Should().ContainSingle(q => q.Key == "public-visitor",
            "a public service request definition runs on exactly the one well-known queue a human ever browses");
        definition.Queues.Should().ContainSingle(q => q.Key == "automation",
            "the automated-decision queue exists purely for the support-system-call leap, never human-visible");
        definition.Stages.Should().HaveCount(6);
    }

    [Fact]
    public void Definition_PassesAuthoringValidation_WithMemberFieldMocked()
    {
        var definition = LoadDefinition();
        var authoringService = new ServiceBlueprintAuthoringService(new Mock<IServiceBlueprintSourceStore>().Object);
        var mockServiceInputs = MemberServiceInputs("Competitive");

        var outcome = authoringService.Validate(definition, mockServiceInputs);

        outcome.IsValid.Should().BeTrue(
            outcome.Diagnostics.Count > 0
                ? string.Join("; ", outcome.Diagnostics.Select(d => $"{d.Code} {d.Path}: {d.Message}"))
                : "expected no diagnostics");
    }

    [Fact]
    public void Definition_PassesAuthoringValidation_WithNoMockAtAll()
    {
        // The scenario the backoffice's own Validation tab actually exercises — no mockServiceInputs,
        // since a real editor there has no way to supply one. Before member declared its own
        // "shape" (Wayfinder.Models.ServiceDesign.Calculations.ServiceBlueprintCalculationField.Shape,
        // added specifically for this), the object-shaped member field, and everything downstream of
        // it (isMember, membershipTier, feeAmount), was permanently unverifiable here — six Warning
        // diagnostics, confirmed live in the backoffice before this was fixed. With shape declared,
        // static validation now has a real placeholder for member.tier and resolves all of them.
        var definition = LoadDefinition();
        var authoringService = new ServiceBlueprintAuthoringService(new Mock<IServiceBlueprintSourceStore>().Object);

        var outcome = authoringService.Validate(definition);

        outcome.Diagnostics.Should().BeEmpty(
            outcome.Diagnostics.Count > 0
                ? string.Join("; ", outcome.Diagnostics.Select(d => $"{d.Code} {d.Path}: {d.Message}"))
                : "expected no diagnostics");
        outcome.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Simulate_AnonymousVisitor_ReachesTheAutomatedDecisionWait_WithUndiscountedFee()
    {
        var definition = LoadDefinition();
        var mockServiceInputs = MemberServiceInputs(tier: "", name: null, email: null);

        var result = new ServiceBlueprintSimulationRunner().Run(
            definition, BuildWalkthroughSteps(overrideLicenceType: "Recreational"), mockServiceInputs);

        result.Trace.Should().HaveCount(5, "initial GetCurrent plus four Advance steps to the automated-decision leap");
        // Submitting leaps into the juggling-licence-decision support system — a real Umbraco
        // Automate callback resolves it, which nothing in this process-isolated simulation can
        // provide, so the trace correctly halts mid-flight, not "complete" (see this class's own
        // remarks on why it's the automation queue's own stage rendering here, not the citizen's
        // waiting screen).
        result.Trace[^1].ResponseState.Should().Be("render");
        result.Trace[^1].Render!.StateDisplayName.Should().Be("Processing your application");
        result.Calculations.Should().OnlyContain(c => c != null,
            "member is always resolved (with an empty tier sentinel for non-members), so calculations never fail");
        result.Calculations[^1]!.Fields["feeAmount"].Should().Be(25m, "a Recreational licence never gets the discount, membership or not");
        result.Calculations[^1]!.Fields["isMember"].Should().Be(false);
    }

    [Fact]
    public void Simulate_LoggedInCompetitiveMember_ReachesTheAutomatedDecisionWait_WithDiscountedFee()
    {
        var definition = LoadDefinition();
        var mockServiceInputs = MemberServiceInputs("Competitive");

        var result = new ServiceBlueprintSimulationRunner().Run(definition, BuildWalkthroughSteps(), mockServiceInputs);

        result.Trace[^1].ResponseState.Should().Be("render");
        result.Trace[^1].Render!.StateDisplayName.Should().Be("Processing your application");
        result.Calculations[^1]!.Fields["isMember"].Should().Be(true);
        result.Calculations[^1]!.Fields["membershipTier"].Should().Be("Competitive");
        result.Calculations[^1]!.Fields["feeAmount"].Should().Be(20m, "applying for a Competitive licence gets the discounted fee");
    }

    [Fact]
    public void Simulate_LoggedInMember_LicenceTypeIsPreFilledFromMembershipTier_BeforeAnySubmission()
    {
        var definition = LoadDefinition();
        var mockServiceInputs = MemberServiceInputs("Professional");

        // Only the first step — stop right before applicant-details would be submitted, so this
        // reads full-name/email-address's own suggested values, not values the walkthrough itself
        // supplied.
        var steps = new[]
        {
            new ProcessManagerSimulationStep("continue", new Dictionary<string, object?>
            {
                ["age-confirmation"] = true,
                ["uk-address-confirmation"] = true
            })
        };

        var result = new ServiceBlueprintSimulationRunner().Run(definition, steps, mockServiceInputs);

        var fields = result.Trace[^1].Render!.Components.SelectMany(c => c.Fields).ToList();

        fields.Single(f => f.FieldKey == "full-name").Value.Should().Be("Alex Juggler",
            "defaultFrom should suggest the member's own name before they've typed anything");
        fields.Single(f => f.FieldKey == "email-address").Value.Should().Be("alex@example.test",
            "defaultFrom should suggest the member's own email before they've typed anything");
    }

    [Fact]
    public void Simulate_LoggedInMember_LicenceTypeIsPreFilledFromMembershipTier_BeforeAnySubmission_OnLicenceStage()
    {
        var definition = LoadDefinition();
        var mockServiceInputs = MemberServiceInputs("Professional");

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
            .Single(f => f.FieldKey == "licenceType");

        licenceTypeField.Value.Should().Be("Professional",
            "defaultFrom should suggest the member's own tier before they've chosen anything");
    }

    [Fact]
    public void Simulate_LoggedInMember_CanOverrideTheSuggestedLicenceType()
    {
        var definition = LoadDefinition();
        var mockServiceInputs = MemberServiceInputs("Professional");

        // Submits "Recreational" despite the member's tier being "Professional" — proves
        // defaultFrom is a genuine, overridable default, not a locked-in value.
        var result = new ServiceBlueprintSimulationRunner().Run(definition, BuildWalkthroughSteps(overrideLicenceType: "Recreational"), mockServiceInputs);

        result.Trace[^1].ResponseState.Should().Be("render");
        result.Trace[^1].Render!.StateDisplayName.Should().Be("Processing your application");

        var checkAnswersEnvelope = result.Trace.First(e => e.Render?.StepType == "check-answers");
        var summaryValue = checkAnswersEnvelope.Render!.Components
            .SelectMany(c => c.Fields)
            .Single(f => f.FieldKey == "licenceType")
            .Value;

        summaryValue.Should().Be("Recreational", "the visitor's own submitted choice always wins over the suggested default");
    }

    [Fact]
    public void Simulate_RecreationalLicenceTypeChosen_DoesNotReceiveTheDiscount()
    {
        var definition = LoadDefinition();
        var mockServiceInputs = MemberServiceInputs("Professional");

        // A Professional-tier member who nonetheless applies for a Recreational licence pays the
        // standard fee — the discount tracks the licence actually applied for, not membership status.
        var result = new ServiceBlueprintSimulationRunner().Run(
            definition, BuildWalkthroughSteps(overrideLicenceType: "Recreational"), mockServiceInputs);

        result.Calculations[^1]!.Fields["isMember"].Should().Be(true);
        result.Calculations[^1]!.Fields["feeAmount"].Should().Be(25m, "the discount only applies to Competitive/Professional licences, whatever the member's own tier");
    }

    [Fact]
    public void Simulate_MembershipTierAloneDoesNotGrantTheDiscount_OnlyTheChosenLicenceTypeDoes()
    {
        var definition = LoadDefinition();
        var mockServiceInputs = MemberServiceInputs("Recreational");

        // A Recreational-tier member who applies for a Competitive licence still gets the
        // discount — the fee is driven entirely by the licence type chosen on this application,
        // never by raw membership status.
        var result = new ServiceBlueprintSimulationRunner().Run(
            definition, BuildWalkthroughSteps(overrideLicenceType: "Competitive"), mockServiceInputs);

        result.Calculations[^1]!.Fields["feeAmount"].Should().Be(20m, "the chosen licence type — not the member's own tier — decides the fee");
    }

    // Mirrors what TestSiteComposer's own serviceInputsResolver actually supplies for a signed-in
    // member: tier from the Juggling Society membership record, name/email from the current
    // claims principal (null for an anonymous visitor, never the anonymous correlation-cookie
    // GUID — see that resolver's own remarks).
    private static Dictionary<string, object?> MemberServiceInputs(string tier, string? name = "Alex Juggler", string? email = "alex@example.test") =>
        new()
        {
            ["member"] = new Dictionary<string, object?> { ["tier"] = tier, ["name"] = name, ["email"] = email }
        };

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
            ["licenceType"] = overrideLicenceType,
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
                "src", "UmbracoPrism.TestSite", "service-blueprints", "apply-for-a-juggling-licence.json");
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
