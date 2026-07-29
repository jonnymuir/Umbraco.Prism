namespace Wayfinder.Models.ServiceDesign.Components;

/// <summary>
/// GDS fieldset component: groups related fields with an optional legend.
/// </summary>
public sealed record FieldsetComponent : PrismComponent
{
    /// <summary>Child components rendered within this fieldset.</summary>
    public IReadOnlyList<PrismComponent> Children { get; init; } = Array.Empty<PrismComponent>();

    /// <summary>Legend text displayed above the fieldset.</summary>
    public string? Legend { get; init; }

    /// <summary>Legend size: "xl" | "l" | "m" | "s".</summary>
    public string? LegendSize { get; init; }
}

/// <summary>
/// GDS accordion component: collapsible sections, each with their own child components.
/// </summary>
public sealed record AccordionComponent : PrismComponent
{
    /// <summary>Sections within this accordion.</summary>
    public IReadOnlyList<AccordionSection> Sections { get; init; } = Array.Empty<AccordionSection>();
}

/// <summary>
/// A single section within an accordion component.
/// </summary>
public sealed record AccordionSection
{
    /// <summary>The section heading.</summary>
    public string Heading { get; init; } = "";

    /// <summary>Optional summary text shown when collapsed.</summary>
    public string? Summary { get; init; }

    /// <summary>Child components rendered within this section.</summary>
    public IReadOnlyList<PrismComponent> Children { get; init; } = Array.Empty<PrismComponent>();
}

/// <summary>
/// GDS panel component: typically used for confirmation messages.
/// </summary>
public sealed record PanelComponent : PrismComponent
{
    /// <summary>Panel heading (title).</summary>
    public string Heading { get; init; } = "";
}
