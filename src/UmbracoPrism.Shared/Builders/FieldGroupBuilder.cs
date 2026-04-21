using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.Shared.Builders;

/// <summary>
/// Fluent builder for creating field groups (form sections) in code with full IntelliSense support.
/// Simplifies the composition of reusable form sections by guiding developers through field addition and configuration.
/// </summary>
/// <remarks>
/// <para>
/// FieldGroupBuilder implements a fluent API for building <see cref="FormSectionDefinition"/> objects.
/// Field groups are reusable collections of form fields that can be used across multiple workflow states,
/// enabling code-driven DRY (Don't Repeat Yourself) configuration of common form sections.
/// </para>
/// <para>
/// Key responsibilities:
/// </para>
/// <list type="bullet">
/// <item>Track the field group's unique key, display name, and version.</item>
/// <item>Collect individual fields using <see cref="AddField"/>.</item>
/// <item>Return a fully-formed <see cref="FormSectionDefinition"/> via <see cref="Build"/>.</item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// var fieldGroup = new FieldGroupBuilder()
///     .Key("personal-info")
///     .DisplayName("Personal Information")
///     .Version(1)
///     .AddField("full-name", f => f
///         .Label("Full name")
///         .FieldType("text")
///         .Required()
///         .MaxLength(100))
///     .AddField("email-address", f => f
///         .Label("Email address")
///         .FieldType("email")
///         .Required()
///         .Hint("We'll use this to contact you"))
///     .AddField("date-of-birth", f => f
///         .Label("Date of birth")
///         .FieldType("date-input")
///         .Required())
///     .Build();
/// </code>
/// </example>
public class FieldGroupBuilder
{
    private string _groupKey = "";
    private string _displayName = "";
    private int _version = 1;
    private readonly List<FieldFile> _fields = new();

    /// <summary>
    /// Sets the unique key for this field group.
    /// </summary>
    /// <param name="groupKey">A unique, URL-safe identifier (e.g., "personal-info", "address").</param>
    /// <returns>This builder instance for method chaining.</returns>
    public FieldGroupBuilder Key(string groupKey)
    {
        _groupKey = groupKey;
        return this;
    }

    /// <summary>
    /// Sets the human-readable display name for this field group.
    /// </summary>
    /// <param name="name">Display name (e.g., "Personal Information", "Address").</param>
    /// <returns>This builder instance for method chaining.</returns>
    public FieldGroupBuilder DisplayName(string name)
    {
        _displayName = name;
        return this;
    }

    /// <summary>
    /// Sets the version number for this field group definition.
    /// </summary>
    /// <param name="version">Version number (default: 1). Increment when changing field structure or validation rules.</param>
    /// <returns>This builder instance for method chaining.</returns>
    public FieldGroupBuilder Version(int version)
    {
        _version = version;
        return this;
    }

    /// <summary>
    /// Adds a field to this field group.
    /// </summary>
    /// <param name="fieldKey">A unique identifier for this field (e.g., "full-name", "email-address").</param>
    /// <param name="configure">A lambda to configure the field (label, type, validation, hints, conditions).</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// The configure lambda receives a <see cref="WorkflowFieldBuilder"/> that allows you to set the field's properties.
    /// Call this method once per field in your group.
    /// </remarks>
    public FieldGroupBuilder AddField(string fieldKey, Action<WorkflowFieldBuilder> configure)
    {
        var builder = new WorkflowFieldBuilder(fieldKey);
        configure(builder);
        _fields.Add(builder.Build());
        return this;
    }

    /// <summary>
    /// Builds and returns a complete field group definition.
    /// </summary>
    /// <returns>A <see cref="FormSectionDefinition"/> with all fields and configuration.</returns>
    /// <remarks>
    /// This method is called after chaining all builder methods.
    /// Ensure all required properties (Key, DisplayName) are set before calling Build().
    /// </remarks>
    public FormSectionDefinition Build()
    {
        return new FormSectionDefinition
        {
            GroupKey = _groupKey,
            DisplayName = _displayName,
            Version = _version,
            Fields = _fields
        };
    }
}

/// <summary>
/// Fluent builder for creating individual form fields within a field group.
/// </summary>
/// <remarks>
/// <para>
/// WorkflowFieldBuilder is used internally by <see cref="FieldGroupBuilder.AddField"/>.
/// It allows detailed configuration of individual form fields, including type, validation, hints, and conditional visibility.
/// </para>
/// <para>
/// Typical usage is via the lambda passed to AddField():
/// <code>
/// .AddField("email-address", f => f
///     .Label("Email address")
///     .FieldType("email")
///     .Required()
///     .Hint("We'll use this to contact you")
///     .MaxLength(254))
/// </code>
/// </para>
/// </remarks>
public class WorkflowFieldBuilder
{
    private readonly string _fieldKey;
    private string _label = "";
    private string? _hint;
    private string _fieldType = "text";
    private bool _required;
    private IReadOnlyList<string>? _options;
    private int? _minLength;
    private int? _maxLength;
    private string? _pattern;
    private decimal? _min;
    private decimal? _max;
    private string? _conditionalOn;
    private string? _visibleWhen;
    private string? _prefix;
    private Dictionary<string, IReadOnlyList<FieldFile>>? _conditionalFields;
    private string? _content;
    private bool _readOnly;

    internal WorkflowFieldBuilder(string fieldKey)
    {
        _fieldKey = fieldKey;
    }

    /// <summary>
    /// Sets the label displayed to users for this field.
    /// </summary>
    /// <param name="label">Human-readable label (e.g., "Email address", "Date of birth").</param>
    /// <returns>This builder instance for method chaining.</returns>
    public WorkflowFieldBuilder Label(string label)
    {
        _label = label;
        return this;
    }

    /// <summary>
    /// Sets the field type, which controls rendering and client-side validation.
    /// </summary>
    /// <param name="type">One of: "text", "email", "textarea", "select", "radio", "checkboxlist", "boolean", "number", "date-input", "inset-text", "warning-text", "details", "notification-banner".</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// Content-only types (inset-text, warning-text, details, notification-banner) do not accept user input.
    /// Set their content via <see cref="Content"/> instead of validation rules.
    /// </remarks>
    public WorkflowFieldBuilder FieldType(string type)
    {
        _fieldType = type;
        return this;
    }

    /// <summary>
    /// Marks this field as required or optional.
    /// </summary>
    /// <param name="required">True to make the field required; false to make it optional (default: true).</param>
    /// <returns>This builder instance for method chaining.</returns>
    public WorkflowFieldBuilder Required(bool required = true)
    {
        _required = required;
        return this;
    }

    /// <summary>
    /// Sets a hint text displayed below the label to guide users.
    /// </summary>
    /// <param name="hint">Hint text (e.g., "We'll use this to contact you").</param>
    /// <returns>This builder instance for method chaining.</returns>
    public WorkflowFieldBuilder Hint(string hint)
    {
        _hint = hint;
        return this;
    }

    /// <summary>
    /// Sets the list of available options for select, radio, or checkboxlist fields.
    /// </summary>
    /// <param name="options">Array of option values.</param>
    /// <returns>This builder instance for method chaining.</returns>
    public WorkflowFieldBuilder Options(params string[] options)
    {
        _options = options;
        return this;
    }

    /// <summary>
    /// Sets the maximum length for text input fields.
    /// </summary>
    /// <param name="max">Maximum character count.</param>
    /// <returns>This builder instance for method chaining.</returns>
    public WorkflowFieldBuilder MaxLength(int max)
    {
        _maxLength = max;
        return this;
    }

    /// <summary>
    /// Sets the minimum length for text input fields.
    /// </summary>
    /// <param name="min">Minimum character count.</param>
    /// <returns>This builder instance for method chaining.</returns>
    public WorkflowFieldBuilder MinLength(int min)
    {
        _minLength = min;
        return this;
    }

    /// <summary>
    /// Sets a regular expression pattern for text validation.
    /// </summary>
    /// <param name="pattern">A regex pattern (e.g., "^[0-9]{3}-[0-9]{2}-[0-9]{4}$" for US Social Security Number format).</param>
    /// <returns>This builder instance for method chaining.</returns>
    public WorkflowFieldBuilder Pattern(string pattern)
    {
        _pattern = pattern;
        return this;
    }

    /// <summary>
    /// Sets the minimum numeric value for number fields.
    /// </summary>
    /// <param name="min">Minimum value.</param>
    /// <returns>This builder instance for method chaining.</returns>
    public WorkflowFieldBuilder Min(decimal min)
    {
        _min = min;
        return this;
    }

    /// <summary>
    /// Sets the maximum numeric value for number fields.
    /// </summary>
    /// <param name="max">Maximum value.</param>
    /// <returns>This builder instance for method chaining.</returns>
    public WorkflowFieldBuilder Max(decimal max)
    {
        _max = max;
        return this;
    }

    /// <summary>
    /// Makes this field conditionally visible based on another field's value.
    /// </summary>
    /// <param name="fieldKey">The key of the field to watch.</param>
    /// <param name="value">The value that makes this field visible (e.g., "yes", "other").</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// Use this for progressive disclosure patterns — show or hide fields based on user answers.
    /// For example, show a "specify other reason" field only when the user selects "other" from a radio group.
    /// </remarks>
    public WorkflowFieldBuilder ShowWhen(string fieldKey, string value)
    {
        _conditionalOn = fieldKey;
        _visibleWhen = value;
        return this;
    }

    /// <summary>
    /// Adds a prefix to the field for display (e.g., "£" for currency, "$" for USD).
    /// </summary>
    /// <param name="prefix">Prefix string to display before the input.</param>
    /// <returns>This builder instance for method chaining.</returns>
    public WorkflowFieldBuilder Prefix(string prefix)
    {
        _prefix = prefix;
        return this;
    }

    /// <summary>
    /// Sets content for non-input field types (inset-text, warning-text, details, notification-banner).
    /// </summary>
    /// <param name="content">HTML or plain text content to display.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// Use for static content fields that do not accept user input.
    /// The step type determines how the content is rendered (e.g., warning-text is styled as a warning).
    /// </remarks>
    public WorkflowFieldBuilder Content(string content)
    {
        _content = content;
        return this;
    }

    /// <summary>
    /// Marks this field as read-only (displayed but not editable).
    /// </summary>
    /// <param name="readOnly">True to make the field read-only; false otherwise (default: true).</param>
    /// <returns>This builder instance for method chaining.</returns>
    public WorkflowFieldBuilder ReadOnly(bool readOnly = true)
    {
        _readOnly = readOnly;
        return this;
    }

    internal FieldFile Build()
    {
        return new FieldFile
        {
            FieldKey = _fieldKey,
            Label = _label,
            Hint = _hint,
            FieldType = _fieldType,
            Required = _required,
            Options = _options,
            MinLength = _minLength,
            MaxLength = _maxLength,
            Pattern = _pattern,
            Min = _min,
            Max = _max,
            ConditionalOn = _conditionalOn,
            VisibleWhen = _visibleWhen,
            Prefix = _prefix,
            ConditionalFields = _conditionalFields,
            Content = _content
        };
    }
}
