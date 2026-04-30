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
