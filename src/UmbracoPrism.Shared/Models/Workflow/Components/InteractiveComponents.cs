namespace UmbracoPrism.Shared.Models.Workflow.Components;

/// <summary>
/// A group of headline statistic tiles (label + value + qualifier), rendered read-only.
/// Values are resolved at render time from instance field values via each item's FieldKey,
/// so the host application controls the figures shown.
/// </summary>
public sealed record StatGroupComponent : PrismComponent
{
    /// <summary>Optional heading rendered above the tiles.</summary>
    public string? Title { get; init; }

    /// <summary>The statistic tiles in display order.</summary>
    public IReadOnlyList<StatItemDefinition> Items { get; init; } = Array.Empty<StatItemDefinition>();
}

/// <summary>
/// A single statistic tile within a <see cref="StatGroupComponent"/>.
/// </summary>
public sealed record StatItemDefinition
{
    /// <summary>Short label above the value (e.g. "DB pension").</summary>
    public string Label { get; init; } = "";

    /// <summary>Instance field key the displayed value is read from at render time.</summary>
    public string FieldKey { get; init; } = "";

    /// <summary>Qualifier text below the value (e.g. "a year, for life").</summary>
    public string? Qualifier { get; init; }

    /// <summary>Whether to render this tile with visual emphasis.</summary>
    public bool Emphasis { get; init; }
}

/// <summary>
/// Mounts a named client-side web component over a set of ordinary input children.
/// The children are the component's outputs contract: they render as a plain form when
/// the element's script has not loaded, and the element drives their values when it has.
/// Form submission therefore always flows through the standard nonce-validated field POST.
/// </summary>
public sealed record InteractiveComponent : PrismComponent
{
    /// <summary>Custom element tag name to mount (e.g. "prism-money-modeller").</summary>
    public string Element { get; init; } = "";

    /// <summary>
    /// Key into the render payload's data bag (<c>StepContent.Data</c>) supplying this
    /// component's model. The resolved JSON is embedded alongside the element at render time.
    /// </summary>
    public string? DataKey { get; init; }

    /// <summary>Fallback/output components — inputs the element reads from and writes back to.</summary>
    public IReadOnlyList<PrismComponent> Children { get; init; } = Array.Empty<PrismComponent>();
}
