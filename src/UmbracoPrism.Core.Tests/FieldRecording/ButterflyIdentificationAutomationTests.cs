using System.Text.Json;
using AwesomeAssertions;
using Umbraco.Automate.Core.Automations;
using UmbracoPrism.TestSite.FieldRecording;
using Wayfinder.Models.ServiceDesign.SupportSystems;

namespace UmbracoPrism.Core.Tests.FieldRecording;

/// <summary>
/// The butterfly-identification automation joins three things that are written in three places: the
/// support system's declared inputs and outputs (appsettings.json), the Umbraco.AI agent's output
/// schema, and the Automate steps that bind one to the other. These tests hold the joins, since a
/// typo in any binding would only show up as a journey that waits forever.
/// </summary>
public class ButterflyIdentificationAutomationTests
{
    static ButterflyIdentificationAutomationTests() => TestSupportSystems.EnsureRegistered();

    private static readonly Guid Workspace = Guid.NewGuid();
    private static readonly Guid Agent = Guid.NewGuid();

    private static Automation Build(string? signingKey = "key") => ButterflyIdentificationAutomation.Build(Workspace, Agent, signingKey);

    private static StepConfiguration RunAgent(Automation a) => a.Steps.Single(s => s.ActionAlias == "umbracoAI.runAgent");

    private static StepConfiguration Resolve(Automation a) => a.Steps.Single(s => s.ActionAlias == "prism.resolveButterflyIdentification");

    [Fact]
    public void TheAutomation_IsAWebhookSignedWithTheSupportSystemsKey()
    {
        var trigger = Build("the-key").Trigger;

        trigger.TriggerAlias.Should().Be("umbracoAutomate.webhook");
        var authenticator = (Dictionary<string, object?>)trigger.Settings["authenticator"]!;
        authenticator["alias"].Should().Be("hmac-sha256");
        ((Dictionary<string, object?>)authenticator["settings"]!)["signingKey"].Should().Be("the-key");
    }

    [Fact]
    public void WithNoKey_TheWebhookFallsBackToAnUnauthenticatedLoopbackOnlyDemo()
    {
        var authenticator = (Dictionary<string, object?>)Build(null).Trigger.Settings["authenticator"]!;

        authenticator["alias"].Should().Be("plain-secret");
    }

    [Fact]
    public void ThePhotoReachesTheAgentAsTheMediaReferenceTheEnvelopeCarries()
    {
        var run = RunAgent(Build());

        run.Settings["attachments"].Should().Be("${trigger.body.inputs.photo.storageKey}");
        run.Settings["agentId"].Should().Be(Agent.ToString());
        run.Settings["toolPermissions"].Should().Be("ReadOnly", "identifying a photo needs no write access to the CMS");
    }

    [Fact]
    public void TheAgentStep_RetriesTwiceWithAPause_BecauseGeminiOverloadsLastLongerThanASingleQuickRetry()
    {
        var run = RunAgent(Build());

        run.ErrorBehavior.Should().Be(StepErrorBehavior.Retry);
        run.MaxRetries.Should().Be(2, "more would spend a quota error's exhausted daily allowance on every attempt");
        run.RetryInterval.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void TheHandBackStep_ReadsEveryValueFromTheAgentStepAndTheInvocationFromTheTrigger()
    {
        var automation = Build();
        var runId = RunAgent(automation).Id;
        var settings = Resolve(automation).Settings;

        settings["invocationId"].Should().Be("${trigger.body.invocationId}");
        settings["agentStepId"].Should().Be(runId.ToString(), "the hand-back reads why the agent step failed, to tell a busy AI service from one that cannot help");
        foreach (var key in new[] { "outcome", "speciesGuess", "commonName", "confidence", "lifeStage", "habitatNotes" })
        {
            settings[key].Should().Be($"${{steps.{runId}.{key}}}", $"{key} must come from the agent's structured answer");
        }
    }

    [Fact]
    public void TheTriggerLeadsToTheAgentWhichLeadsToTheHandBack()
    {
        var automation = Build();
        var connections = automation.Connections.Select(c => (c.SourceStepId, c.TargetStepId)).ToList();

        connections.Should().BeEquivalentTo(new[]
        {
            (Guid.Empty, RunAgent(automation).Id),
            (RunAgent(automation).Id, Resolve(automation).Id),
        });
    }

    [Fact]
    public void TheAgentsOutputSchema_CoversEverythingTheHandBackReadsAndTheSupportSystemReturns()
    {
        var schema = FieldRecordingAiSetup.OutputSchema.GetProperty("properties");
        var schemaNames = schema.EnumerateObject().Select(p => p.Name).ToList();
        var capability = SupportSystemRegistry.FindCapability(ButterflyIdentificationAutomation.SupportSystemKey, "identify-butterfly")!;

        schemaNames.Should().Contain(capability.Outputs.Select(o => o.Key), "each declared output must be something the agent is asked for");
        schemaNames.Should().Contain("outcome");
    }

    [Fact]
    public void TheAgentsOutcomeEnum_IsExactlyTheSetThePayloadAndTheBlueprintAccept_PlusBusy_WhichOnlyTheHandBackDecides()
    {
        var enumValues = FieldRecordingAiSetup.OutputSchema.GetProperty("properties").GetProperty("outcome").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString()).ToList();
        var declared = SupportSystemRegistry.FindCapability(ButterflyIdentificationAutomation.SupportSystemKey, "identify-butterfly")!
            .Outcomes.Select(o => o.Key).ToList();

        enumValues.Should().BeEquivalentTo(ButterflyIdentificationPayload.Outcomes);
        enumValues.Should().NotContain(ButterflyIdentificationPayload.Busy, "a model that answered was not busy");
        declared.Should().BeEquivalentTo(ButterflyIdentificationPayload.Outcomes.Append(ButterflyIdentificationPayload.Busy));
    }

    [Fact]
    public void TheAgentsInstructions_TellItToTreatNotesAsDataAndNeverToGuess()
    {
        FieldRecordingAiSetup.Instructions.Should().Contain("never instructions", "the notes are practitioner-supplied text")
            .And.Contain("Do not guess a species");
    }

    [Fact]
    public void EveryInputTheSupportSystemDeclares_ReachesTheAgent()
    {
        var message = (string)RunAgent(Build()).Settings["message"]!;
        var capability = SupportSystemRegistry.FindCapability(ButterflyIdentificationAutomation.SupportSystemKey, "identify-butterfly")!;

        foreach (var input in capability.Inputs.Where(i => i.Key != "photo"))
        {
            message.Should().Contain($"${{trigger.body.inputs.{input.Key}}}", $"{input.Key} is sent, so the agent must be told it");
        }
    }

    [Fact]
    public void TheAgentsInstructions_LetALookAgainHintSayWhereToLook_ButNeverChangeTheTask()
    {
        FieldRecordingAiSetup.Instructions.Should().Contain("Use that only to decide where to look")
            .And.Contain("never makes you more certain than the photo allows");
    }

    [Fact]
    public void TheAgentsOutputSchema_IsValidJson_WithOnlyOutcomeRequired()
    {
        var schema = FieldRecordingAiSetup.OutputSchema;

        schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()).Should().BeEquivalentTo("outcome");
        JsonSerializer.Serialize(schema).Should().NotBeNullOrWhiteSpace();
    }
}
