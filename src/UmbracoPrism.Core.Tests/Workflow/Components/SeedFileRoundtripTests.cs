using System.Text.Json;
using FluentAssertions;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.Shared.Models.Workflow.Components;
using Xunit;

namespace UmbracoPrism.Core.Tests.Workflow.Components;

/// <summary>
/// Regression guard: ensures all workflow seed JSONs conform to the v2.0 polymorphic schema.
/// Prevents recurrence of the v1 "fields[]" + "fieldType" shape in seed files.
/// </summary>
public class SeedFileRoundtripTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// The editor's canonical-JSON serialiser alphabetically sorts all keys, placing the
    /// polymorphic "type" discriminator AFTER sibling properties (e.g. after "label",
    /// "fieldKey", "legend"). The save endpoint uses AllowOutOfOrderMetadataProperties
    /// so the backend must tolerate this ordering.
    /// </summary>
    private static readonly JsonSerializerOptions EditorSaveOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        AllowOutOfOrderMetadataProperties = true,
    };

    /// <summary>
    /// Validates that all seed JSONs deserialize correctly into v2 WorkflowDefinitionFile
    /// and have structurally valid components (no orphaned v1 properties).
    /// </summary>
    [Theory]
    [InlineData("community-enquiry.json")]
    [InlineData("payment-demo.json")]
    [InlineData("planning-notification.json")]
    [InlineData("information-request.json")]
    public void SeedFile_RoundtripsSuccessfully(string seedFileName)
    {
        // Arrange
        var seedPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "UmbracoPrism.MockBusinessApp",
            "workflow-seeds",
            seedFileName
        );

        seedPath = Path.GetFullPath(seedPath);
        File.Exists(seedPath).Should().BeTrue($"seed file {seedFileName} should exist at {seedPath}");

        var json = File.ReadAllText(seedPath);

        // Act
        WorkflowDefinitionFile definition;
        try
        {
            definition = JsonSerializer.Deserialize<WorkflowDefinitionFile>(json, JsonOptions)!;
        }
        catch (JsonException ex)
        {
            Assert.Fail($"Seed file {seedFileName} failed to deserialize: {ex.Message}");
            return;
        }

        // Assert: basic structure
        definition.Should().NotBeNull();
        definition.DefinitionKey.Should().NotBeNullOrEmpty();
        definition.States.Should().NotBeNullOrEmpty("every workflow needs at least one state");

        // Assert: every state has components
        foreach (var state in definition.States)
        {
            state.StateKey.Should().NotBeNullOrEmpty();
            state.Components.Should().NotBeNullOrEmpty($"state '{state.StateKey}' should have at least one component");
        }

        // Assert: no fieldsets with orphaned v1 "Fields" property (they should use "Children")
        ValidateNoOrphanedV1Schema(definition, seedFileName);

        // Assert: all input components are properly typed
        ValidateInputComponentsHaveProperTypes(definition, seedFileName);
    }

    private static void ValidateNoOrphanedV1Schema(WorkflowDefinitionFile definition, string fileName)
    {
        var allComponents = definition.States.SelectMany(s => FlattenComponents(s.Components)).ToList();

        foreach (var component in allComponents.OfType<FieldsetComponent>())
        {
            component.Children.Should().NotBeNullOrEmpty(
                $"Fieldset in {fileName} must have non-empty Children (v1 'fields[]' property is obsolete)"
            );
        }
    }

    private static void ValidateInputComponentsHaveProperTypes(WorkflowDefinitionFile definition, string fileName)
    {
        var allComponents = definition.States.SelectMany(s => FlattenComponents(s.Components)).ToList();

        foreach (var component in allComponents.OfType<InputComponent>())
        {
            component.FieldKey.Should().NotBeNullOrEmpty(
                $"Input component in {fileName} must have a FieldKey"
            );

            component.Label.Should().NotBeNullOrEmpty(
                $"Input component '{component.FieldKey}' in {fileName} must have a Label"
            );

            // Ensure we got the right polymorphic subtype (not the abstract base)
            component.GetType().Should().NotBe(typeof(InputComponent),
                $"Component '{component.FieldKey}' in {fileName} should deserialize to a concrete InputComponent subtype, not the abstract base"
            );
        }
    }

    /// <summary>
    /// Recursively flattens all components in a tree (including nested children in fieldsets, accordions, etc.)
    /// </summary>
    private static IEnumerable<PrismComponent> FlattenComponents(IEnumerable<PrismComponent> components)
    {
        foreach (var component in components)
        {
            yield return component;

            // Recurse into container children
            if (component is FieldsetComponent fs)
            {
                foreach (var child in FlattenComponents(fs.Children))
                    yield return child;
            }

            if (component is AccordionComponent ac)
            {
                foreach (var section in ac.Sections)
                {
                    foreach (var child in FlattenComponents(section.Children))
                        yield return child;
                }
            }

            // Recurse into conditional children (Radios/Checkboxes)
            if (component is RadiosComponent radio && radio.ConditionalChildren != null)
            {
                foreach (var conditionalSet in radio.ConditionalChildren.Values)
                {
                    foreach (var child in FlattenComponents(conditionalSet))
                        yield return child;
                }
            }

            if (component is CheckboxesComponent checkbox && checkbox.ConditionalChildren != null)
            {
                foreach (var conditionalSet in checkbox.ConditionalChildren.Values)
                {
                    foreach (var child in FlattenComponents(conditionalSet))
                        yield return child;
                }
            }
        }
    }
}

public class EditorCanonicalJsonRoundtripTests
{
    private static readonly JsonSerializerOptions EditorSaveOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        AllowOutOfOrderMetadataProperties = true,
    };

    /// <summary>
    /// Verifies that the editor's canonical serialisation format — where JSON object keys
    /// are sorted alphabetically, placing "type" after all other properties — can be
    /// round-tripped through the save endpoint's deserialiser without losing component
    /// content. This is the root-cause regression guard for the payment-demo field-binding
    /// bug: if AllowOutOfOrderMetadataProperties is ever removed or misconfigured, this
    /// test will catch the failure before it reaches the runtime.
    /// </summary>
    [Fact]
    public void EditorCanonicalJson_WithTypeDiscriminatorLast_RoundtripsComponentLabels()
    {
        // Arrange: editor-canonical JSON has all keys sorted alphabetically.
        // "type" appears AFTER "fieldKey", "label", "legend", "required", etc.
        const string canonicalJson = """
            {
              "definitionKey": "payment-demo",
              "displayName": "Payment Demo",
              "gateways": [],
              "initialState": "enter-details",
              "instancePolicy": "single",
              "queues": [{ "displayName": "Applicant", "key": "web-user" }],
              "states": [
                {
                  "actor": "applicant",
                  "actions": [],
                  "components": [
                    {
                      "children": [
                        {
                          "fieldKey": "cardholderName",
                          "label": "Card Number",
                          "required": true,
                          "type": "text"
                        },
                        {
                          "fieldKey": "amount",
                          "label": "Amount (\u00a3)",
                          "required": true,
                          "type": "decimal"
                        }
                      ],
                      "legend": "Enter Payment Details",
                      "type": "fieldset"
                    }
                  ],
                  "displayName": "Enter payment details",
                  "queueKey": "web-user",
                  "roleGates": [],
                  "routes": [],
                  "stageType": "Question",
                  "stateKey": "enter-details"
                }
              ],
              "version": 1
            }
            """;

        // Act: deserialise using the same options as the save endpoint
        WorkflowDefinitionFile? definition;
        try
        {
            definition = JsonSerializer.Deserialize<WorkflowDefinitionFile>(canonicalJson, EditorSaveOptions);
        }
        catch (JsonException ex)
        {
            Assert.Fail($"Editor canonical JSON failed to deserialise: {ex.Message}");
            return;
        }

        // Assert: components survive the round-trip with the updated label
        definition.Should().NotBeNull();
        var enterDetails = definition!.States.Should().ContainSingle(s => s.StateKey == "enter-details").Subject;
        var fieldset = enterDetails.Components.Should().ContainSingle(c => c is FieldsetComponent).Subject as FieldsetComponent;
        fieldset.Should().NotBeNull();

        var cardholderField = fieldset!.Children
            .OfType<InputComponent>()
            .Should().ContainSingle(c => c.FieldKey == "cardholderName")
            .Subject;

        cardholderField.Label.Should().Be(
            "Card Number",
            "the editor-saved label change must survive C# deserialization when type discriminator appears last");

        // Confirm concrete type (not abstract base)
        cardholderField.Should().BeOfType<TextInputComponent>();
    }
}
