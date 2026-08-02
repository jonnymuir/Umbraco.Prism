using FluentAssertions;
using Wayfinder.Models.ServiceDesign.Components;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Components;

public class ComponentTypeCatalogTests
{
    // The full, exhaustive set of [JsonDerivedType] discriminators actually declared on
    // Component today. Cross-checked directly against Component.cs's attribute
    // list, not just against ComponentPolymorphismTests's [Theory] data (which doesn't cover
    // slider/stat-group/chart).
    private static readonly string[] ExpectedDiscriminators =
    [
        "accordion", "body", "boolean", "chart", "checkboxlist", "date", "decimal", "details",
        "email", "fieldset", "file-upload", "guidance-checklist", "heading", "inset-text",
        "notification-banner", "number", "panel", "radio", "select", "slider", "stat-group",
        "summary-list", "task-list", "text", "textarea", "waiting", "warning-text"
    ];

    [Fact]
    public void AllDiscriminators_MatchesTheKnownGoodSet()
    {
        ComponentTypeCatalog.AllDiscriminators.Should().BeEquivalentTo(ExpectedDiscriminators);
    }

    [Fact]
    public void AllDiscriminators_DoesNotIncludeTel()
    {
        // TelComponent exists as a C# type but has no [JsonDerivedType] entry on Component
        // — it's already dead/unregistered, and stays that way; this test guards against it
        // being accidentally reintroduced without deliberately deciding to fix it.
        ComponentTypeCatalog.AllDiscriminators.Should().NotContain("tel");
    }

    [Fact]
    public void DiscriminatorFor_KnownComponent_ReturnsItsDiscriminator()
    {
        ComponentTypeCatalog.DiscriminatorFor(new TextInputComponent { FieldKey = "x", Label = "X" })
            .Should().Be("text");
        ComponentTypeCatalog.DiscriminatorFor(new FieldsetComponent())
            .Should().Be("fieldset");
        ComponentTypeCatalog.DiscriminatorFor(new SummaryListComponent())
            .Should().Be("summary-list");
    }
}
