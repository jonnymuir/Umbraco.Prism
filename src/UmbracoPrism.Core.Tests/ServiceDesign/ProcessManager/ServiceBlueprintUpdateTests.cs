using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using UmbracoPrism.MockBusinessApp.Services;
using Wayfinder.Models.ServiceDesign;
using Wayfinder.Models.ServiceDesign.Components;
using Wayfinder.Services.Sanitization;
using Xunit;

namespace UmbracoPrism.Core.Tests.ServiceDesign.ProcessManager;

/// <summary>
/// Regression guard: verifies that UpdateDefinition propagates authoring changes to the runtime
/// engine. Previously, saving via the PUT /mockapp/service-blueprints/{key} endpoint only updated
/// ReferenceWorkflowSourceStore and left the engine's in-memory definitions stale.
/// </summary>
public class WorkflowDefinitionUpdateTests : IDisposable
{
    private readonly string _testSeedDir;
    private readonly BusinessAppProcessManager _engine;

    public WorkflowDefinitionUpdateTests()
    {
        _testSeedDir = Path.Combine(Directory.GetCurrentDirectory(), $"test-seeds-update-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testSeedDir);
        var seedsDir = Path.Combine(_testSeedDir, "service-blueprints");
        Directory.CreateDirectory(seedsDir);

        WriteSeedFile(seedsDir, "payment-demo", BuildPaymentDemoSeed("Cardholder name"));

        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.ContentRootPath).Returns(_testSeedDir);

        var logger = new Mock<ILogger<BusinessAppProcessManager>>();
        var sanitizer = new Mock<IServiceContentSanitizer>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string?>())).Returns<string?>(h => h ?? string.Empty);

        _engine = new BusinessAppProcessManager(logger.Object, mockEnv.Object, sanitizer.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testSeedDir))
            Directory.Delete(_testSeedDir, recursive: true);
    }

    [Fact]
    public void UpdateDefinition_ReturnsTrue_ForKnownKey()
    {
        var updated = BuildPaymentDemoSeed("Cardholder ID");

        var result = _engine.UpdateDefinition("payment-demo", updated);

        result.Should().BeTrue("engine should accept an update for a key it already holds");
    }

    [Fact]
    public void UpdateDefinition_RegistersNewKey_NotLoadedAtStartup()
    {
        // A brand-new workflow key (one an agent or human just authored from scratch via
        // save_workflow) must actually become servable — otherwise "a save reaches the live
        // engine immediately" is false for exactly the scenario — authoring a new service —
        // the toolkit exists for.
        var authored = BuildPaymentDemoSeed("Cardholder ID") with { DefinitionKey = "brand-new-workflow" };

        var result = _engine.UpdateDefinition("brand-new-workflow", authored);

        result.Should().BeTrue("a new key must be registered, not silently rejected");
        _engine.GetDefinition("brand-new-workflow").Should().NotBeNull(
            "the newly-registered definition must be immediately servable");
    }

    [Fact]
    public void UpdateDefinition_TextInputLabel_IsReflectedByGetDefinition()
    {
        var updated = BuildPaymentDemoSeed("Cardholder ID");

        _engine.UpdateDefinition("payment-demo", updated);

        var loaded = _engine.GetDefinition("payment-demo");
        loaded.Should().NotBeNull();

        var cardholderField = FindInputByFieldKey(loaded!, "cardholderName");
        cardholderField.Should().NotBeNull("cardholderName field should survive the update round-trip");
        cardholderField!.Label.Should().Be(
            "Cardholder ID",
            "label change saved via UpdateDefinition must be visible to the runtime immediately");
    }

    [Fact]
    public void UpdateDefinition_DoesNotAffectOriginalSeedLabel_BeforeUpdate()
    {
        var initial = _engine.GetDefinition("payment-demo");

        var cardholderField = FindInputByFieldKey(initial!, "cardholderName");
        cardholderField!.Label.Should().Be(
            "Cardholder name",
            "engine should load the original seed label at startup");
    }

    private static ServiceBlueprint BuildPaymentDemoSeed(string cardholderLabel) =>
        new()
        {
            DefinitionKey = "payment-demo",
            DisplayName = "Payment Demo",
            Version = 1,
            InitialStage = "enter-details",
            RequestPolicy = "single",
            Stages = [
                new StageDefinition
                {
                    StageKey = "enter-details",
                    DisplayName = "Enter payment details",
                    Components =
                    [
                        new FieldsetComponent
                        {
                            Legend = "Payment details",
                            Children =
                            [
                                new TextInputComponent
                                {
                                    FieldKey = "cardholderName",
                                    Label = cardholderLabel,
                                    Required = true
                                }
                            ]
                        }
                    ],
                    Routes =
                    [
                        new ServiceBlueprintRouteDefinition { Id = "route-1", Target = "done", Trigger = "submit" }
                    ]
                },
                new StageDefinition
                {
                    StageKey = "done",
                    DisplayName = "Done",
                    Components = [new PanelComponent { Heading = "Complete" }]
                }
            ]
        };

    private static void WriteSeedFile(string directory, string key, ServiceBlueprint workflow)
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        var json = System.Text.Json.JsonSerializer.Serialize(workflow, options);
        File.WriteAllText(Path.Combine(directory, $"{key}.json"), json);
    }

    private static InputComponent? FindInputByFieldKey(ServiceBlueprint definition, string fieldKey)
    {
        foreach (var state in definition.Stages)
        {
            var match = FindInComponents(state.Components, fieldKey);
            if (match is not null)
                return match;
        }
        return null;
    }

    private static InputComponent? FindInComponents(IEnumerable<Component> components, string fieldKey)
    {
        foreach (var component in components)
        {
            if (component is InputComponent input && input.FieldKey == fieldKey)
                return input;

            if (component is FieldsetComponent fieldset)
            {
                var nested = FindInComponents(fieldset.Children, fieldKey);
                if (nested is not null) return nested;
            }
        }
        return null;
    }
}
