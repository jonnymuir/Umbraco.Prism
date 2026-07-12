namespace UmbracoPrism.Shared.Models.Workflow.Components;

/// <summary>
/// Abstract base for all input field components, carrying common field properties.
/// </summary>
public abstract record InputComponent : PrismComponent
{
    /// <summary>Unique identifier for this field (e.g., "full-name").</summary>
    public string FieldKey { get; init; } = "";

    /// <summary>User-facing label displayed next to the field.</summary>
    public string Label { get; init; } = "";

    /// <summary>Optional hint or helper text displayed below the label.</summary>
    public string? Hint { get; init; }

    /// <summary>Whether this field must be completed before submission.</summary>
    public bool Required { get; init; }

    /// <summary>The field key this field depends on for visibility.</summary>
    public string? ConditionalOn { get; init; }

    /// <summary>The value that makes this field visible when ConditionalOn is set.</summary>
    public string? VisibleWhen { get; init; }

    /// <summary>
    /// Default value used when the instance has no saved value for this field —
    /// pre-populates the rendered control and seeds the calculation scope.
    /// </summary>
    public string? Default { get; init; }

    /// <summary>
    /// When this component appears as a summary-list row, the state key its own "Change" link
    /// navigates back to — overriding the summary-list's own <c>ChangeStateKey</c> for rows
    /// summarising fields captured on a different earlier stage. Ignored outside a summary-list.
    /// </summary>
    public string? ChangeStateKey { get; init; }
}

/// <summary>
/// GDS text input component.
/// </summary>
public sealed record TextInputComponent : InputComponent
{
    /// <summary>Minimum character length.</summary>
    public int? MinLength { get; init; }

    /// <summary>Maximum character length.</summary>
    public int? MaxLength { get; init; }

    /// <summary>HTML5 pattern (regex) attribute value.</summary>
    public string? Pattern { get; init; }

    /// <summary>Currency/unit prefix displayed before the input (e.g., "£").</summary>
    public string? Prefix { get; init; }
}

/// <summary>
/// GDS number input component (integer values).
/// </summary>
public sealed record NumberInputComponent : InputComponent
{
    /// <summary>Minimum value.</summary>
    public decimal? Min { get; init; }

    /// <summary>Maximum value.</summary>
    public decimal? Max { get; init; }

    /// <summary>Currency/unit prefix displayed before the input (e.g., "£").</summary>
    public string? Prefix { get; init; }
}

/// <summary>
/// GDS decimal input component (floating-point values).
/// </summary>
public sealed record DecimalInputComponent : InputComponent
{
    /// <summary>Minimum value.</summary>
    public decimal? Min { get; init; }

    /// <summary>Maximum value.</summary>
    public decimal? Max { get; init; }

    /// <summary>Currency/unit prefix displayed before the input (e.g., "£").</summary>
    public string? Prefix { get; init; }
}

/// <summary>
/// GDS select dropdown component.
/// </summary>
public sealed record SelectComponent : InputComponent
{
    /// <summary>Available options for the dropdown.</summary>
    public IReadOnlyList<string> Options { get; init; } = Array.Empty<string>();
}

/// <summary>
/// GDS radios component: radio button group with optional conditional child components.
/// </summary>
public sealed record RadiosComponent : InputComponent
{
    /// <summary>Available radio options.</summary>
    public IReadOnlyList<string> Options { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Optional conditional child components revealed when specific options are selected.
    /// Key is the option value; value is the list of components shown when that option is active.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<PrismComponent>>? ConditionalChildren { get; init; }
}

/// <summary>
/// GDS checkboxes component: checkbox group.
/// </summary>
public sealed record CheckboxesComponent : InputComponent
{
    /// <summary>Available checkbox options.</summary>
    public IReadOnlyList<string> Options { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Optional conditional child components revealed when specific options are selected.
    /// Key is the option value; value is the list of components shown when that option is active.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<PrismComponent>>? ConditionalChildren { get; init; }
}

/// <summary>
/// GDS date input component.
/// </summary>
public sealed record DateInputComponent : InputComponent
{
}

/// <summary>
/// GDS email input component.
/// </summary>
public sealed record EmailComponent : InputComponent
{
    /// <summary>HTML5 pattern (regex) attribute value.</summary>
    public string? Pattern { get; init; }
}

/// <summary>
/// GDS telephone input component.
/// </summary>
public sealed record TelComponent : InputComponent
{
    /// <summary>HTML5 pattern (regex) attribute value.</summary>
    public string? Pattern { get; init; }
}

/// <summary>
/// GDS textarea component: multi-line text input.
/// </summary>
public sealed record TextareaComponent : InputComponent
{
    /// <summary>Minimum character length.</summary>
    public int? MinLength { get; init; }

    /// <summary>Maximum character length.</summary>
    public int? MaxLength { get; init; }
}

/// <summary>
/// GDS boolean/checkbox component: single yes/no checkbox.
/// </summary>
public sealed record BooleanComponent : InputComponent
{
}

/// <summary>
/// Range slider input. Renders as a native range control with its current value
/// displayed alongside; submits like a number field.
/// </summary>
public sealed record SliderComponent : InputComponent
{
    /// <summary>Minimum value.</summary>
    public decimal? Min { get; init; }

    /// <summary>Maximum value.</summary>
    public decimal? Max { get; init; }

    /// <summary>Step between selectable values (e.g. 0.5).</summary>
    public decimal? Step { get; init; }

    /// <summary>Currency/unit prefix displayed before the value (e.g., "£").</summary>
    public string? Prefix { get; init; }

    /// <summary>Unit suffix displayed after the value (e.g., "%").</summary>
    public string? Suffix { get; init; }
}
