using UmbracoPrism.Shared.Models.ServiceDesign;
using UmbracoPrism.Shared.Models.ServiceDesign.Components;

namespace UmbracoPrism.Shared.Builders;

/// <summary>
/// Fluent builder for creating v2.0 service blueprints in code with full IntelliSense support.
/// Emits the polymorphic <see cref="PrismComponent"/> hierarchy (text input, radios, fieldset, panel, etc.)
/// rather than the legacy V1 <c>FieldFile</c>-based shape.
/// </summary>
/// <example>
/// <code>
/// var blueprint = new ServiceBlueprintBuilder()
///     .Key("pension-application")
///     .DisplayName("Pension Application")
///     .Version(1)
///     .StartsAt("details")
///     .AddState("details", s => s
///         .DisplayName("Your Details")
///         .Fieldset(f => f
///             .Legend("Personal info", "l")
///             .TextInput("name", "Full name", required: true)
///             .Email("email", "Email address", required: true))
///         .Radios("contact-method", "How should we contact you?",
///             new[] { "email", "phone", "other" },
///             conditional: c => c.When("other", o => o
///                 .TextInput("contact-other", "Tell us how", required: true))))
///     .AddState("submitted", s => s
///         .DisplayName("Application submitted")
///         .Panel("Application submitted")
///         .Body("We will contact you within 5 working days."))
///     .AddTransition("details", "submitted", "submit")
///     .Build();
/// </code>
/// </example>
public class ServiceBlueprintBuilder
{
    private string _definitionKey = "";
    private string _displayName = "";
    private int _version = 1;
    private string _initialState = "";
    private string _instancePolicy = "single";
    private readonly List<StepDefinition> _states = new();
    private readonly List<RouteFile> _transitions = new();

    /// <summary>Sets the unique key for this service blueprint.</summary>
    public ServiceBlueprintBuilder Key(string definitionKey) { _definitionKey = definitionKey; return this; }

    /// <summary>Sets the human-readable display name for this blueprint.</summary>
    public ServiceBlueprintBuilder DisplayName(string name) { _displayName = name; return this; }

    /// <summary>Sets the version number for this service blueprint.</summary>
    public ServiceBlueprintBuilder Version(int version) { _version = version; return this; }

    /// <summary>Sets the key of the touchpoint where users enter the blueprint.</summary>
    public ServiceBlueprintBuilder StartsAt(string touchpointKey) { _initialState = touchpointKey; return this; }

    /// <summary>Sets the instance creation policy: "single" | "multiple" | "prompt".</summary>
    public ServiceBlueprintBuilder RequestPolicy(string policy) { _instancePolicy = policy; return this; }

    /// <summary>Adds a touchpoint to this service blueprint.</summary>
    public ServiceBlueprintBuilder AddState(string touchpointKey, Action<StateBuilder> configure)
    {
        var builder = new StateBuilder(touchpointKey);
        configure(builder);
        _states.Add(builder.Build());
        return this;
    }

    /// <summary>Adds a transition (edge) between two touchpoints.</summary>
    public ServiceBlueprintBuilder AddTransition(string fromState, string toState, string action, string? requiresRole = null)
    {
        _transitions.Add(new RouteFile
        {
            FromState = fromState,
            ToState = toState,
            Action = action,
            RequiresRole = requiresRole
        });
        return this;
    }

    /// <summary>Builds and returns a complete service blueprint file.</summary>
    public ServiceBlueprint Build() => new()
    {
        DefinitionKey = _definitionKey,
        DisplayName = _displayName,
        Version = _version,
        InitialTouchpoint = _initialState,
        RequestPolicy = _instancePolicy,
        Touchpoints = _states.ToArray(),
        Transitions = _transitions.ToArray()
    };
}

/// <summary>
/// Shared fluent base providing the 12 input components and content/container helpers
/// used by both <see cref="StateBuilder"/> and <see cref="FieldsetBuilder"/>.
/// CRTP-style <typeparamref name="TSelf"/> preserves the most-derived type for chaining.
/// </summary>
public abstract class ComponentCollectionBuilder<TSelf> where TSelf : ComponentCollectionBuilder<TSelf>
{
    protected readonly List<PrismComponent> Components = new();

    protected abstract TSelf Self { get; }

    /// <summary>Appends an arbitrary <see cref="PrismComponent"/> to this collection.</summary>
    public TSelf Add(PrismComponent component)
    {
        Components.Add(component);
        return Self;
    }

    // ---- Container components ----

    /// <summary>Adds a fieldset container with its own nested children.</summary>
    public TSelf Fieldset(Action<FieldsetBuilder> configure)
    {
        var b = new FieldsetBuilder();
        configure(b);
        Components.Add(b.Build());
        return Self;
    }

    /// <summary>Adds a summary-list (check-answers) component referencing existing field keys.</summary>
    public TSelf SummaryList(Action<SummaryListBuilder> configure)
    {
        var b = new SummaryListBuilder();
        configure(b);
        Components.Add(b.Build());
        return Self;
    }

    /// <summary>Adds a panel component (typically used on confirmation pages).</summary>
    public TSelf Panel(string heading)
    {
        Components.Add(new PanelComponent { Heading = heading });
        return Self;
    }

    // ---- Input components ----

    /// <summary>Adds a single-line text input.</summary>
    public TSelf TextInput(string fieldKey, string label, bool required = false, string? hint = null,
        int? minLength = null, int? maxLength = null, string? pattern = null, string? prefix = null,
        string? conditionalOn = null, string? visibleWhen = null)
    {
        Components.Add(new TextInputComponent
        {
            FieldKey = fieldKey, Label = label, Required = required, Hint = hint,
            MinLength = minLength, MaxLength = maxLength, Pattern = pattern, Prefix = prefix,
            ConditionalOn = conditionalOn, VisibleWhen = visibleWhen
        });
        return Self;
    }

    /// <summary>Adds an integer number input.</summary>
    public TSelf NumberInput(string fieldKey, string label, bool required = false, string? hint = null,
        decimal? min = null, decimal? max = null, string? prefix = null,
        string? conditionalOn = null, string? visibleWhen = null)
    {
        Components.Add(new NumberInputComponent
        {
            FieldKey = fieldKey, Label = label, Required = required, Hint = hint,
            Min = min, Max = max, Prefix = prefix,
            ConditionalOn = conditionalOn, VisibleWhen = visibleWhen
        });
        return Self;
    }

    /// <summary>Adds a decimal/floating-point number input.</summary>
    public TSelf DecimalInput(string fieldKey, string label, bool required = false, string? hint = null,
        decimal? min = null, decimal? max = null, string? prefix = null,
        string? conditionalOn = null, string? visibleWhen = null)
    {
        Components.Add(new DecimalInputComponent
        {
            FieldKey = fieldKey, Label = label, Required = required, Hint = hint,
            Min = min, Max = max, Prefix = prefix,
            ConditionalOn = conditionalOn, VisibleWhen = visibleWhen
        });
        return Self;
    }

    /// <summary>Adds a select dropdown.</summary>
    public TSelf Select(string fieldKey, string label, IEnumerable<string> options,
        bool required = false, string? hint = null,
        string? conditionalOn = null, string? visibleWhen = null)
    {
        Components.Add(new SelectComponent
        {
            FieldKey = fieldKey, Label = label, Required = required, Hint = hint,
            Options = options.ToArray(),
            ConditionalOn = conditionalOn, VisibleWhen = visibleWhen
        });
        return Self;
    }

    /// <summary>Adds a radio button group, optionally with conditional child components per option.</summary>
    public TSelf Radios(string fieldKey, string label, IEnumerable<string> options,
        bool required = false, string? hint = null,
        Action<ConditionalChildrenBuilder>? conditional = null,
        string? conditionalOn = null, string? visibleWhen = null)
    {
        IReadOnlyDictionary<string, IReadOnlyList<PrismComponent>>? children = null;
        if (conditional is not null)
        {
            var b = new ConditionalChildrenBuilder();
            conditional(b);
            children = b.Build();
        }
        Components.Add(new RadiosComponent
        {
            FieldKey = fieldKey, Label = label, Required = required, Hint = hint,
            Options = options.ToArray(),
            ConditionalChildren = children,
            ConditionalOn = conditionalOn, VisibleWhen = visibleWhen
        });
        return Self;
    }

    /// <summary>Adds a checkbox group, optionally with conditional child components per option.</summary>
    public TSelf Checkboxes(string fieldKey, string label, IEnumerable<string> options,
        bool required = false, string? hint = null,
        Action<ConditionalChildrenBuilder>? conditional = null,
        string? conditionalOn = null, string? visibleWhen = null)
    {
        IReadOnlyDictionary<string, IReadOnlyList<PrismComponent>>? children = null;
        if (conditional is not null)
        {
            var b = new ConditionalChildrenBuilder();
            conditional(b);
            children = b.Build();
        }
        Components.Add(new CheckboxesComponent
        {
            FieldKey = fieldKey, Label = label, Required = required, Hint = hint,
            Options = options.ToArray(),
            ConditionalChildren = children,
            ConditionalOn = conditionalOn, VisibleWhen = visibleWhen
        });
        return Self;
    }

    /// <summary>Adds a date input (day/month/year).</summary>
    public TSelf DateInput(string fieldKey, string label, bool required = false, string? hint = null,
        string? conditionalOn = null, string? visibleWhen = null)
    {
        Components.Add(new DateInputComponent
        {
            FieldKey = fieldKey, Label = label, Required = required, Hint = hint,
            ConditionalOn = conditionalOn, VisibleWhen = visibleWhen
        });
        return Self;
    }

    /// <summary>Adds an email input.</summary>
    public TSelf Email(string fieldKey, string label, bool required = false, string? hint = null,
        string? pattern = null,
        string? conditionalOn = null, string? visibleWhen = null)
    {
        Components.Add(new EmailComponent
        {
            FieldKey = fieldKey, Label = label, Required = required, Hint = hint,
            Pattern = pattern,
            ConditionalOn = conditionalOn, VisibleWhen = visibleWhen
        });
        return Self;
    }

    /// <summary>Adds a telephone input.</summary>
    public TSelf Tel(string fieldKey, string label, bool required = false, string? hint = null,
        string? pattern = null,
        string? conditionalOn = null, string? visibleWhen = null)
    {
        Components.Add(new TelComponent
        {
            FieldKey = fieldKey, Label = label, Required = required, Hint = hint,
            Pattern = pattern,
            ConditionalOn = conditionalOn, VisibleWhen = visibleWhen
        });
        return Self;
    }

    /// <summary>Adds a multi-line text area.</summary>
    public TSelf Textarea(string fieldKey, string label, bool required = false, string? hint = null,
        int? minLength = null, int? maxLength = null,
        string? conditionalOn = null, string? visibleWhen = null)
    {
        Components.Add(new TextareaComponent
        {
            FieldKey = fieldKey, Label = label, Required = required, Hint = hint,
            MinLength = minLength, MaxLength = maxLength,
            ConditionalOn = conditionalOn, VisibleWhen = visibleWhen
        });
        return Self;
    }

    /// <summary>Adds a single yes/no checkbox.</summary>
    public TSelf Boolean(string fieldKey, string label, bool required = false, string? hint = null,
        string? conditionalOn = null, string? visibleWhen = null)
    {
        Components.Add(new BooleanComponent
        {
            FieldKey = fieldKey, Label = label, Required = required, Hint = hint,
            ConditionalOn = conditionalOn, VisibleWhen = visibleWhen
        });
        return Self;
    }

    // ---- Content helpers ----

    /// <summary>Adds a body (paragraph) component.</summary>
    public TSelf Body(string content)
    {
        Components.Add(new BodyComponent { Content = content });
        return Self;
    }

    /// <summary>Adds a heading component (level 1-6).</summary>
    public TSelf Heading(int level, string content)
    {
        Components.Add(new HeadingComponent { Level = level, Content = content });
        return Self;
    }

    /// <summary>Adds an inset-text component.</summary>
    public TSelf InsetText(string content)
    {
        Components.Add(new InsetTextComponent { Content = content });
        return Self;
    }

    /// <summary>Adds a warning-text component.</summary>
    public TSelf WarningText(string content)
    {
        Components.Add(new WarningTextComponent { Content = content });
        return Self;
    }

    /// <summary>Adds a collapsible details component.</summary>
    public TSelf Details(string heading, string content)
    {
        Components.Add(new DetailsComponent { Heading = heading, Content = content });
        return Self;
    }

    /// <summary>Adds a notification banner ("info" | "success" | "warning").</summary>
    public TSelf NotificationBanner(string bannerType, string heading, string content)
    {
        Components.Add(new NotificationBannerComponent { BannerType = bannerType, Heading = heading, Content = content });
        return Self;
    }

    /// <summary>Adds a waiting component for paused/long-running blueprints.</summary>
    public TSelf Waiting(string content, int expectedWaitSeconds, int pollIntervalMs = 3000,
        bool allowDefer = true, string? deferMessage = null)
    {
        Components.Add(new WaitingComponent
        {
            Content = content,
            ExpectedWaitSeconds = expectedWaitSeconds,
            PollIntervalMs = pollIntervalMs,
            AllowDefer = allowDefer,
            DeferMessage = deferMessage
        });
        return Self;
    }
}

/// <summary>Builder for a blueprint touchpoint's <see cref="PrismComponent"/> tree.</summary>
public sealed class StateBuilder : ComponentCollectionBuilder<StateBuilder>
{
    private readonly string _stateKey;
    private string _displayName = "";

    internal StateBuilder(string touchpointKey) { _stateKey = touchpointKey; }

    protected override StateBuilder Self => this;

    /// <summary>Sets the human-readable display name for this touchpoint.</summary>
    public StateBuilder DisplayName(string name) { _displayName = name; return this; }

    internal StepDefinition Build() => new()
    {
        TouchpointKey = _stateKey,
        DisplayName = _displayName,
        Components = Components.ToArray()
    };
}

/// <summary>Builder for a <see cref="FieldsetComponent"/> with its own nested components.</summary>
public sealed class FieldsetBuilder : ComponentCollectionBuilder<FieldsetBuilder>
{
    private string? _legend;
    private string? _legendSize;

    protected override FieldsetBuilder Self => this;

    /// <summary>Sets the fieldset legend and optional size ("xl" | "l" | "m" | "s").</summary>
    public FieldsetBuilder Legend(string legend, string? size = null)
    {
        _legend = legend;
        _legendSize = size;
        return this;
    }

    internal FieldsetComponent Build() => new()
    {
        Legend = _legend,
        LegendSize = _legendSize,
        Children = Components.ToArray()
    };
}

/// <summary>
/// Builder used by <see cref="ComponentCollectionBuilder{TSelf}.Radios"/> and
/// <see cref="ComponentCollectionBuilder{TSelf}.Checkboxes"/> to associate option values with
/// nested child components ("conditional reveals").
/// </summary>
public sealed class ConditionalChildrenBuilder
{
    private readonly Dictionary<string, IReadOnlyList<PrismComponent>> _map = new();

    /// <summary>
    /// Registers child components revealed when <paramref name="optionValue"/> is selected.
    /// </summary>
    public ConditionalChildrenBuilder When(string optionValue, Action<ChildrenBuilder> configure)
    {
        var b = new ChildrenBuilder();
        configure(b);
        _map[optionValue] = b.BuildChildren();
        return this;
    }

    internal IReadOnlyDictionary<string, IReadOnlyList<PrismComponent>>? Build()
        => _map.Count == 0 ? null : _map;
}

/// <summary>
/// Plain bag of <see cref="PrismComponent"/>s for contexts that aren't a fieldset or touchpoint
/// (e.g., conditional children of a radio option).
/// </summary>
public sealed class ChildrenBuilder : ComponentCollectionBuilder<ChildrenBuilder>
{
    protected override ChildrenBuilder Self => this;

    internal IReadOnlyList<PrismComponent> BuildChildren() => Components.ToArray();
}

/// <summary>Builder for a <see cref="SummaryListComponent"/>.</summary>
public sealed class SummaryListBuilder
{
    private readonly ChildrenBuilder _children = new();
    private string? _changeStateKey;
    private string? _title;

    /// <summary>
    /// Adds inline input definitions to summarise. The summary-list carries its own
    /// field schemas so labels, formatting, options and conditional reveals are all
    /// captured directly on the component.
    /// </summary>
    public SummaryListBuilder Children(Action<ChildrenBuilder> configure)
    {
        configure(_children);
        return this;
    }

    /// <summary>The touchpoint key the "Change" links navigate to.</summary>
    public SummaryListBuilder ChangeStateKey(string touchpointKey) { _changeStateKey = touchpointKey; return this; }

    /// <summary>Optional heading shown above the summary list.</summary>
    public SummaryListBuilder Title(string title) { _title = title; return this; }

    internal SummaryListComponent Build() => new()
    {
        Children = _children.BuildChildren(),
        ChangeStateKey = _changeStateKey,
        Title = _title
    };
}
