namespace Wayfinder.Models.ServiceDesign.Components;

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
/// Declarative chart bound to a calculation series. The server renders it statically
/// (bars, legend and an accessible data table); the live-form runtime re-renders it
/// as inputs change. Currently supported kind: "stacked-bar".
/// </summary>
public sealed record ChartComponent : PrismComponent
{
    /// <summary>Chart heading shown above the plot.</summary>
    public string? Title { get; init; }

    /// <summary>Chart kind. Currently "stacked-bar".</summary>
    public string Kind { get; init; } = "stacked-bar";

    /// <summary>Name of the calculation series supplying the rows.</summary>
    public string Series { get; init; } = "";

    /// <summary>Series column used for the x axis (typically the loop variable).</summary>
    public string X { get; init; } = "";

    /// <summary>Stacked bands in draw order (bottom first).</summary>
    public IReadOnlyList<ChartBand> Bands { get; init; } = Array.Empty<ChartBand>();

    /// <summary>Label interval on the x axis (e.g. 5 → label every 5th value). Default 5.</summary>
    public int XLabelEvery { get; init; } = 5;
}

/// <summary>One stacked band of a chart, bound to a series column.</summary>
public sealed record ChartBand
{
    /// <summary>Series column name supplying this band's values.</summary>
    public string Key { get; init; } = "";

    /// <summary>Legend label.</summary>
    public string Label { get; init; } = "";

    /// <summary>
    /// Optional hex colour. When omitted the renderer assigns from its validated
    /// categorical palette in band order.
    /// </summary>
    public string? Color { get; init; }
}
