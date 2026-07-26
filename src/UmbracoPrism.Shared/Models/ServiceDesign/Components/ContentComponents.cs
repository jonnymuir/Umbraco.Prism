namespace UmbracoPrism.Shared.Models.ServiceDesign.Components;

/// <summary>
/// GDS body component: renders paragraph text content.
/// </summary>
public sealed record BodyComponent : PrismComponent
{
    /// <summary>Body text content.</summary>
    public string Content { get; init; } = "";
}

/// <summary>
/// GDS heading component: renders a heading at a specified level.
/// </summary>
public sealed record HeadingComponent : PrismComponent
{
    /// <summary>Heading level (1-6).</summary>
    public int Level { get; init; } = 2;

    /// <summary>Heading text content.</summary>
    public string Content { get; init; } = "";
}

/// <summary>
/// GDS inset text component: highlights important content in an inset box.
/// </summary>
public sealed record InsetTextComponent : PrismComponent
{
    /// <summary>Inset text content.</summary>
    public string Content { get; init; } = "";
}

/// <summary>
/// GDS warning text component: displays a warning message with an exclamation icon.
/// </summary>
public sealed record WarningTextComponent : PrismComponent
{
    /// <summary>Warning text content.</summary>
    public string Content { get; init; } = "";
}

/// <summary>
/// GDS details component: expandable/collapsible section.
/// </summary>
public sealed record DetailsComponent : PrismComponent
{
    /// <summary>Summary text displayed when collapsed (the clickable heading).</summary>
    public string Heading { get; init; } = "";

    /// <summary>Content revealed when expanded.</summary>
    public string Content { get; init; } = "";
}

/// <summary>
/// GDS notification banner component: prominent banner for important messages.
/// </summary>
public sealed record NotificationBannerComponent : PrismComponent
{
    /// <summary>Banner type: "info" | "success" | "warning".</summary>
    public string BannerType { get; init; } = "info";

    /// <summary>Banner heading.</summary>
    public string Heading { get; init; } = "";

    /// <summary>Banner body content.</summary>
    public string Content { get; init; } = "";
}
