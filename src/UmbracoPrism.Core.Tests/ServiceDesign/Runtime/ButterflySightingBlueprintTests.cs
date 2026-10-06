using System.Text.Json;
using AwesomeAssertions;
using Moq;
using Wayfinder.Engine.Abstractions;
using Wayfinder.Engine.Services;
using Wayfinder.Models.ServiceDesign;
using Wayfinder.Models.ServiceDesign.Components;
using Wayfinder.Models.ServiceDesign.SupportSystems;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Runtime;

/// <summary>
/// The "Record a butterfly sighting" field-recording service: one practitioner queue that starts it
/// and does all the work, and a system queue that hands the photo to the butterfly-identification
/// support system (an Umbraco Automate automation asking an Umbraco.AI agent). These tests hold the
/// blueprint to its own configuration: the support system is registered from the real
/// <c>appsettings.json</c> (see <see cref="TestSupportSystems"/>), so a mismatch between what the
/// blueprint sends and what the configuration declares fails here.
/// </summary>
public class ButterflySightingBlueprintTests
{
    private const string Slug = "record-a-butterfly-sighting";
    private const string SupportSystemKey = "butterfly-identification";
    private const string CapabilityKey = "identify-butterfly";

    static ButterflySightingBlueprintTests() => TestSupportSystems.EnsureRegistered();

    [Fact]
    public void Definition_RunsOnOnePractitionerQueueAndOneSystemQueue()
    {
        var definition = LoadDefinition();

        definition.DefinitionKey.Should().Be(Slug);
        definition.Queues.Should().ContainSingle(q => q.Key == "public-visitor" && q.Actor == "applicant",
            "the practitioner starts the service and does all the work, with no frontstage and backstage split");
        definition.Queues.Should().ContainSingle(q => q.Key == "identification" && q.Actor == "system",
            "the support system is its own queue that hands off to Automate, never human-visible");
        definition.Queues.Should().HaveCount(2);
    }

    [Fact]
    public void Definition_PassesAuthoringValidationWithNoDiagnostics()
    {
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
    public void ThePhotoField_AllowsTheCameraOrAnExistingPhoto_AndOnlyImagesTheAiModelAccepts()
    {
        var photo = AllComponents(LoadDefinition()).OfType<FileUploadComponent>().Should().ContainSingle().Subject;

        photo.FieldKey.Should().Be("sightingPhoto");
        photo.Required.Should().BeTrue();
        photo.CaptureMode.Should().Be("choose", "a practitioner may upload a photo taken earlier");
        photo.AcceptedFileTypes.Should().BeEquivalentTo(".jpg", ".jpeg", ".png", ".webp");
        photo.MaxSizeBytes.Should().Be(10 * 1024 * 1024);
    }

    [Fact]
    public void TheLocationField_IsARequiredLocationPicker()
    {
        var location = AllComponents(LoadDefinition()).OfType<LocationPickerComponent>().Should().ContainSingle().Subject;

        location.FieldKey.Should().Be("location");
        location.Required.Should().BeTrue();
    }

    [Fact]
    public void TheIdentifyStage_SendsEachDeclaredInputFromARealFieldOnTheForm()
    {
        var definition = LoadDefinition();
        var call = definition.Stages.Single(s => s.StageKey == "identify").Actions!.Single(a => a.Type == "support-system-call");
        var inputs = call.Parameters!["inputs"]!.AsObject().ToDictionary(p => p.Key, p => p.Value!.GetValue<string>());
        var formFields = AllComponents(definition).OfType<InputComponent>().Select(c => c.FieldKey).ToHashSet();

        call.Parameters["supportSystemKey"]!.GetValue<string>().Should().Be(SupportSystemKey);
        call.Parameters["capabilityKey"]!.GetValue<string>().Should().Be(CapabilityKey);
        inputs["photo"].Should().Be("sightingPhoto");
        inputs.Values.Should().OnlyContain(field => formFields.Contains(field),
            "a typo here would send nothing and the identification would have no photo");
    }

    [Fact]
    public void EveryOutcomeTheConfigurationDeclares_HasARouteOutOfTheIdentifyStageAndTheJoinGateway()
    {
        var definition = LoadDefinition();
        var declared = SupportSystemRegistry.Find(SupportSystemKey)!.Capabilities.Single(c => c.Key == CapabilityKey)
            .Outcomes.Select(o => o.Key).ToList();

        declared.Should().BeEquivalentTo("identified", "unclear", "not-a-butterfly");
        definition.Stages.Single(s => s.StageKey == "identify").Routes.Select(r => r.Trigger)
            .Should().BeEquivalentTo(declared, "an outcome with no route would strand the practitioner at the wait screen");
        definition.Gateways.Single(g => g.Key == "identification-complete").Routes.Select(r => r.Trigger)
            .Should().BeEquivalentTo(declared);
    }

    [Fact]
    public void TheConfirmStage_OnlyDefaultsFromValuesTheSupportSystemActuallyReturns()
    {
        var definition = LoadDefinition();
        var outputs = SupportSystemRegistry.Find(SupportSystemKey)!.Capabilities.Single(c => c.Key == CapabilityKey)
            .Outputs.Select(o => o.Key).ToHashSet();
        var confirm = definition.Stages.Single(s => s.StageKey == "confirm-sighting");

        var defaults = AllComponents(confirm).OfType<InputComponent>().Where(c => c.DefaultFrom is not null).ToList();

        defaults.Should().NotBeEmpty("the practitioner confirms a suggestion, so the fields start from it");
        defaults.Should().OnlyContain(c => outputs.Contains(c.DefaultFrom!));
        definition.Calculations!.Fields.Keys.Should().Contain(defaults.Select(c => c.DefaultFrom!),
            "a default can only read a calculation field, so each suggestion it uses must be passed through as a service field");
    }

    [Fact]
    public void Simulate_SubmittingTheSighting_LeapsIntoTheIdentificationAndWaits()
    {
        var definition = LoadDefinition();
        var steps = new[]
        {
            new ProcessManagerSimulationStep("continue", new Dictionary<string, object?>
            {
                ["sightingPhoto"] = new ServiceRequestFileReference { StorageKey = "umb://media/1f8204160c6c45e482ad0a393c1567e6", OriginalFileName = "peacock.jpg", ContentType = "image/jpeg", SizeBytes = 1000 },
                ["location"] = "51.752000,-1.257700",
                ["sightingDate"] = "2026-10-05",
            }),
        };

        var result = ServiceBlueprintSimulationRunner.Run(definition, steps, new Dictionary<string, object?>());

        result.Trace.Should().HaveCount(2, "the initial render plus one Advance into the identification");
        result.Trace[0].Render!.StateDisplayName.Should().Be("Record your sighting");
        result.Trace[^1].ResponseState.Should().Be("render");
        result.Trace[^1].Render!.StateDisplayName.Should().Be("Identifying the butterfly",
            "the simulation harness shows the system queue's own stage, as for the juggling licence; a real practitioner waits at the join gateway");
    }

    private static IEnumerable<Component> AllComponents(ServiceBlueprint definition) =>
        definition.Stages.SelectMany(AllComponents);

    private static IEnumerable<Component> AllComponents(StageDefinition stage) =>
        stage.Components.SelectMany(Flatten);

    private static IEnumerable<Component> Flatten(Component component) =>
        component is FieldsetComponent fieldset
            ? new[] { component }.Concat(fieldset.Children.SelectMany(Flatten))
            : [component];

    private static ServiceBlueprint LoadDefinition()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "UmbracoPrism.TestSite", "service-blueprints", $"{Slug}.json");
            if (File.Exists(candidate))
            {
                return JsonSerializer.Deserialize<ServiceBlueprint>(File.ReadAllText(candidate), ServiceBlueprintJson.ReadOptions)
                    ?? throw new InvalidOperationException("Deserialized to null.");
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find {Slug}.json walking up from {Directory.GetCurrentDirectory()}.");
    }
}
