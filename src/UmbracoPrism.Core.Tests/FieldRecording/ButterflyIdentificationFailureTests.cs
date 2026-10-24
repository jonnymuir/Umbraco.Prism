using AwesomeAssertions;
using Umbraco.Automate.Core.Actions;
using UmbracoPrism.TestSite.FieldRecording;

namespace UmbracoPrism.Core.Tests.FieldRecording;

/// <summary>
/// When the AI agent gives no answer, why it failed decides what the practitioner is offered: a busy
/// or rate-limited AI service is worth trying again shortly, anything else is not.
/// </summary>
public class ButterflyIdentificationFailureTests
{
    private static readonly ResolveButterflyIdentificationSettings NoAnswer = new() { InvocationId = "inv-1" };

    [Theory]
    [InlineData(StepRunErrorCategory.ServiceUnavailable)]
    [InlineData(StepRunErrorCategory.RateLimiting)]
    [InlineData(StepRunErrorCategory.Timeout)]
    public void AnAgentThatGaveUpBecauseTheAiServiceWasBusy_IsBusy_SoThePractitionerCanTryAgain(StepRunErrorCategory failure)
    {
        var result = ButterflyIdentificationPayload.From(NoAnswer, failure);

        result.Outcome.Should().Be(ButterflyIdentificationPayload.Busy);
        result.Degraded.Should().BeTrue();
        result.Payload["speciesGuess"]!.GetValue<string>().Should().BeEmpty();
        result.Payload["habitatNotes"]!.GetValue<string>().Should().BeEmpty("the busy screen says what happened, so no note is carried into the record");
    }

    [Theory]
    [InlineData(StepRunErrorCategory.Authentication)]
    [InlineData(StepRunErrorCategory.ConfigurationError)]
    [InlineData(StepRunErrorCategory.Unknown)]
    public void AnAgentThatFailedForAReasonAskingAgainCannotFix_IsUnclear_SoThePractitionerEntersIt(StepRunErrorCategory failure)
    {
        var result = ButterflyIdentificationPayload.From(NoAnswer, failure);

        result.Outcome.Should().Be("unclear");
        result.Payload["habitatNotes"]!.GetValue<string>().Should().Be(ButterflyIdentificationPayload.UnavailableNote);
    }

    [Fact]
    public void AUsableAnswer_IsKept_EvenIfAnEarlierAttemptWasBusy()
    {
        var answered = new ResolveButterflyIdentificationSettings { InvocationId = "inv-1", Outcome = "identified", CommonName = "Peacock" };

        var result = ButterflyIdentificationPayload.From(answered, StepRunErrorCategory.ServiceUnavailable);

        result.Outcome.Should().Be("identified");
        result.Degraded.Should().BeFalse();
    }
}
