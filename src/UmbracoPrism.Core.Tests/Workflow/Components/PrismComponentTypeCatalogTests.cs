using FluentAssertions;
using UmbracoPrism.Shared.Models.Workflow.Components;

namespace UmbracoPrism.Core.Tests.Workflow.Components;

public class PrismComponentTypeCatalogTests
{
    // The full, exhaustive set of [JsonDerivedType] discriminators actually declared on
    // PrismComponent today. Cross-checked directly against PrismComponent.cs's attribute
    // list, not just against ComponentPolymorphismTests's [Theory] data (which doesn't cover
    // slider/stat-group/chart).
    private static readonly string[] ExpectedDiscriminators =
    [
        "accordion", "body", "boolean", "chart", "checkboxlist", "date", "decimal", "details",
        "email", "fieldset", "heading", "inset-text", "notification-banner", "number", "panel",
        "radio", "select", "slider", "stat-group", "summary-list", "task-list", "text",
        "textarea", "waiting", "warning-text"
    ];

    [Fact]
    public void AllDiscriminators_MatchesTheKnownGoodSet()
    {
        PrismComponentTypeCatalog.AllDiscriminators.Should().BeEquivalentTo(ExpectedDiscriminators);
    }

    [Fact]
    public void AllDiscriminators_DoesNotIncludeTel()
    {
        // TelComponent exists as a C# type but has no [JsonDerivedType] entry on PrismComponent
        // — it's already dead/unregistered, and stays that way; this test guards against it
        // being accidentally reintroduced without deliberately deciding to fix it.
        PrismComponentTypeCatalog.AllDiscriminators.Should().NotContain("tel");
    }

    [Fact]
    public void DiscriminatorFor_KnownComponent_ReturnsItsDiscriminator()
    {
        PrismComponentTypeCatalog.DiscriminatorFor(new TextInputComponent { FieldKey = "x", Label = "X" })
            .Should().Be("text");
        PrismComponentTypeCatalog.DiscriminatorFor(new FieldsetComponent())
            .Should().Be("fieldset");
        PrismComponentTypeCatalog.DiscriminatorFor(new SummaryListComponent())
            .Should().Be("summary-list");
    }
}
