using UmbracoPrism.Shared.Models.ServiceDesign;

namespace UmbracoPrism.Core.Models.ServiceDesign;

/// <summary>
/// View model passed to each <c>_Component-{Type}.cshtml</c> field partial.
/// Pre-computes ARIA attributes, CSS classes, and conditional wrappers
/// so field partials stay declarative and free of C# logic.
/// </summary>
public record PrismFieldContext
{
    /// <summary>The raw field definition from the workflow engine.</summary>
    public required FieldRenderPayload Field { get; init; }

    /// <summary>Validation error for this field, if any.</summary>
    public string? FieldError { get; init; }

    /// <summary>
    /// The current workflow instance id — needed by a <c>file-upload</c> field's partial to
    /// build a link back to its own uploaded file. Empty outside a rendering context that
    /// forwards it (see <c>PrismComponentTagHelper.ProcessFieldAsync</c>).
    /// </summary>
    public string InstanceId { get; init; } = string.Empty;

    /// <summary>
    /// The current step's nonce — needed by a <c>file-upload</c> field's partial to authorize
    /// its own async upload request against the same authoritative-fields check
    /// <c>HandlePost</c> already performs for the whole-page submission.
    /// </summary>
    public string Nonce { get; init; } = string.Empty;

    /// <summary>The current workflow key — needed by a <c>file-upload</c> field's async upload URL.</summary>
    public string BlueprintKey { get; init; } = string.Empty;

    /// <summary>
    /// The value to display in the field — resolved from submitted values,
    /// then DefaultValue, then the engine-provided Value.
    /// </summary>
    public string DisplayValue { get; init; } = string.Empty;

    // -- Derived state --

    public bool HasFieldError => !string.IsNullOrEmpty(FieldError);
    public bool HasHint       => !string.IsNullOrEmpty(Field.Hint);
    public string HintId      => $"{Field.FieldKey}-hint";
    public string ErrorId     => $"{Field.FieldKey}-error";

    // -- ARIA / HTML attributes (pre-built strings, Html.Raw-safe) --

    /// <summary>Pre-built aria-describedby="..." attribute or empty string.</summary>
    public string DescribedBy { get; init; } = string.Empty;

    public string RequiredAttr     => Field.Required   ? " required"                           : string.Empty;
    public string AriaRequired     => Field.Required   ? @" aria-required=""true"""            : string.Empty;
    public string AriaInvalid      => HasFieldError    ? @" aria-invalid=""true"""             : string.Empty;
    public string ReadOnlyAttr     => Field.ReadOnly   ? @" readonly aria-readonly=""true"""   : string.Empty;
    public string ReadOnlyCssClass => Field.ReadOnly   ? " govuk-input--readonly"              : string.Empty;

    // -- Constraint attributes --

    public string MinLengthAttr => Field.MinLength.HasValue ? $@" minlength=""{Field.MinLength.Value}""" : string.Empty;
    public string MaxLengthAttr => Field.MaxLength.HasValue ? $@" maxlength=""{Field.MaxLength.Value}""" : string.Empty;
    public string PatternAttr   => !string.IsNullOrEmpty(Field.Pattern)
                                    ? $@" pattern=""{System.Net.WebUtility.HtmlEncode(Field.Pattern)}"""
                                    : string.Empty;
    public string MinAttr       => Field.Min.HasValue ? $@" min=""{Field.Min.Value}""" : string.Empty;
    public string MaxAttr       => Field.Max.HasValue ? $@" max=""{Field.Max.Value}""" : string.Empty;
    public string StepAttr      => Field.Step.HasValue ? $@" step=""{Field.Step.Value}""" : string.Empty;

    // -- Wrapper div (govuk-form-group + conditional) --

    /// <summary>CSS classes for the outer govuk-form-group wrapper div.</summary>
    public string WrapperClass { get; init; } = "govuk-form-group";

    /// <summary>
    /// Extra HTML attributes for the wrapper div — conditional field data attributes
    /// and hidden / aria-hidden when the field is conditionally hidden.
    /// Already HTML-encoded and safe for @Html.Raw().
    /// </summary>
    public string WrapperAttrs { get; init; } = string.Empty;

    // -- Factory --

    public static PrismFieldContext Build(
        FieldRenderPayload field,
        string? fieldError,
        IReadOnlyDictionary<string, string>? values,
        string instanceId = "",
        string nonce = "",
        string blueprintKey = "")
    {
        var submittedValue = values?.GetValueOrDefault(field.FieldKey);
        var displayValue   = !string.IsNullOrWhiteSpace(field.DefaultValue)
            ? field.DefaultValue
            : submittedValue ?? field.Value?.ToString() ?? string.Empty;

        // Strip prefix from display value (prefix rendered separately in input wrapper)
        if (!string.IsNullOrEmpty(field.Prefix) &&
            displayValue.StartsWith(field.Prefix, StringComparison.Ordinal))
        {
            displayValue = displayValue[field.Prefix.Length..];
        }

        var hasHint  = !string.IsNullOrEmpty(field.Hint);
        var hasError = !string.IsNullOrEmpty(fieldError);
        var fieldKey = field.FieldKey;

        var encodedFieldKey = System.Net.WebUtility.HtmlEncode(fieldKey);
        var describedByParts = new List<string>();
        if (hasHint)  describedByParts.Add($"{encodedFieldKey}-hint");
        if (hasError) describedByParts.Add($"{encodedFieldKey}-error");
        var describedBy = describedByParts.Count > 0
            ? $@" aria-describedby=""{string.Join(" ", describedByParts)}"""
            : string.Empty;

        var isConditional = !string.IsNullOrEmpty(field.ConditionalOn);
        var wrapperClass  = "govuk-form-group"
                          + (hasError       ? " govuk-form-group--error" : string.Empty)
                          + (isConditional  ? " prism-field--conditional" : string.Empty);

        var wrapperAttrs = isConditional
            ? $@" data-conditional-on=""{System.Net.WebUtility.HtmlEncode(field.ConditionalOn)}"" data-visible-when=""{System.Net.WebUtility.HtmlEncode(field.VisibleWhen ?? "")}"" hidden aria-hidden=""true"""
            : string.Empty;

        return new PrismFieldContext
        {
            Field        = field,
            FieldError   = fieldError,
            DisplayValue = displayValue,
            DescribedBy  = describedBy,
            WrapperClass = wrapperClass,
            WrapperAttrs = wrapperAttrs,
            InstanceId   = instanceId,
            Nonce        = nonce,
            BlueprintKey  = blueprintKey,
        };
    }
}
