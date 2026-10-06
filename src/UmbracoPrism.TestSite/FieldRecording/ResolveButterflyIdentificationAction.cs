using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Settings;
using Wayfinder.Umbraco.Services;

namespace UmbracoPrism.TestSite.FieldRecording;

/// <summary>Settings for <see cref="ResolveButterflyIdentificationAction"/>.</summary>
public sealed class ResolveButterflyIdentificationSettings
{
    [Field(Label = "Invocation id", Description = "The invocationId from the trigger body.", SupportsBindings = true)]
    public string InvocationId { get; set; } = string.Empty;

    [Field(Label = "Outcome", Description = "identified, unclear or not-a-butterfly.", SortOrder = 1, SupportsBindings = true)]
    public string Outcome { get; set; } = string.Empty;

    [Field(Label = "Scientific name", SortOrder = 2, SupportsBindings = true)]
    public string? SpeciesGuess { get; set; }

    [Field(Label = "Common name", SortOrder = 3, SupportsBindings = true)]
    public string? CommonName { get; set; }

    [Field(Label = "Confidence", Description = "low, medium or high.", SortOrder = 4, SupportsBindings = true)]
    public string? Confidence { get; set; }

    [Field(Label = "Life stage", SortOrder = 5, SupportsBindings = true)]
    public string? LifeStage { get; set; }

    [Field(Label = "Habitat and surroundings", SortOrder = 6, SupportsBindings = true)]
    public string? HabitatNotes { get; set; }
}

/// <summary>
/// Turns an AI agent's answer into the outcome and result payload for the butterfly-identification
/// support system. The answer is model output, so it is treated as untrusted text: it is assembled
/// with <see cref="JsonObject"/> (never interpolated into JSON text), the outcome and confidence are
/// restricted to fixed sets, control characters are stripped and lengths are capped.
/// </summary>
/// <remarks>
/// When the agent produced no usable outcome (the AI service was overloaded or unreachable and
/// Automate skipped past the failed step) the result degrades to <c>unclear</c> with a note, so the
/// practitioner can still type what they saw, instead of waiting forever for an answer that is
/// never coming.
/// </remarks>
public static class ButterflyIdentificationPayload
{
    public sealed record Result(string Outcome, JsonObject Payload, bool Degraded);

    public static readonly IReadOnlyList<string> Outcomes = ["identified", "unclear", "not-a-butterfly"];
    private static readonly string[] Confidences = ["low", "medium", "high"];

    public const int ShortTextLimit = 150;
    public const int NotesLimit = 600;
    public const string UnavailableNote = "No suggestion was available. Enter what you saw yourself.";

    public static Result From(ResolveButterflyIdentificationSettings settings)
    {
        var outcome = (settings.Outcome ?? string.Empty).Trim();
        if (!Outcomes.Contains(outcome, StringComparer.Ordinal))
        {
            return new Result("unclear", Payload(string.Empty, string.Empty, string.Empty, string.Empty, UnavailableNote), Degraded: true);
        }

        return new Result(
            outcome,
            Payload(
                Clean(settings.SpeciesGuess, ShortTextLimit),
                Clean(settings.CommonName, ShortTextLimit),
                Confidence(settings.Confidence),
                Clean(settings.LifeStage, ShortTextLimit),
                Clean(settings.HabitatNotes, NotesLimit)),
            Degraded: false);
    }

    private static JsonObject Payload(string species, string common, string confidence, string lifeStage, string notes) => new()
    {
        ["speciesGuess"] = species,
        ["commonName"] = common,
        ["confidence"] = confidence,
        ["lifeStage"] = lifeStage,
        ["habitatNotes"] = notes,
    };

    private static string Confidence(string? value)
    {
        var lowered = (value ?? string.Empty).Trim().ToLowerInvariant();
        return Confidences.Contains(lowered, StringComparer.Ordinal) ? lowered : string.Empty;
    }

    private static string Clean(string? value, int limit)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(Math.Min(value.Length, limit));
        foreach (var c in value.Trim())
        {
            if (builder.Length == limit)
            {
                break;
            }

            builder.Append(char.IsControl(c) ? ' ' : c);
        }

        return builder.ToString().Trim();
    }
}

/// <summary>
/// Resolves the butterfly-identification support system in process with the agent's answer.
/// A typed counterpart to <see cref="ResolveWayfinderSupportSystemOutcomeAction"/> that takes each
/// output as its own setting, so no model-generated text is ever spliced into JSON.
/// </summary>
[Action("prism.resolveButterflyIdentification", "Resolve butterfly identification",
    Description = "Resolves a waiting butterfly-identification invocation in process, with the species suggestion as individual fields.",
    Group = "Wayfinder",
    Icon = "icon-checkbox")]
public sealed class ResolveButterflyIdentificationAction(
    ActionInfrastructure infrastructure,
    UmbracoProcessManagerEngine engine,
    ILogger<ResolveButterflyIdentificationAction> logger)
    : ActionBase<ResolveButterflyIdentificationSettings, ResolveButterflyIdentificationAction.Output>(infrastructure)
{
    public sealed class Output
    {
        public string ResponseState { get; set; } = string.Empty;
    }

    public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<ResolveButterflyIdentificationSettings>();

        if (string.IsNullOrWhiteSpace(settings.InvocationId))
        {
            return Task.FromResult(ActionResult.Failed(
                new ArgumentException("An invocationId is required."),
                StepRunErrorCategory.Validation));
        }

        var result = ButterflyIdentificationPayload.From(settings);
        if (result.Degraded)
        {
            logger.LogWarning(
                "Butterfly identification {InvocationId} had no usable outcome from the AI agent; resolving as unclear so the practitioner can enter it.",
                settings.InvocationId);
        }

        var resolution = SupportSystemOutcomeResolver.Resolve(engine, logger, settings.InvocationId, result.Outcome, result.Payload);

        return Task.FromResult(resolution.Error is null
            ? Success(new Output { ResponseState = resolution.ResponseState })
            : ActionResult.Failed(new InvalidOperationException(resolution.Error), StepRunErrorCategory.InvalidResponse));
    }
}
