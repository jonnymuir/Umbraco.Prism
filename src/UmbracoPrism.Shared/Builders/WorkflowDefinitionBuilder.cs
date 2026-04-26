using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.Shared.Builders;

/// <summary>
/// Fluent builder for creating workflow definitions in code with full IntelliSense support.
/// Simplifies definition composition by guiding developers through all required properties and optional configurations.
/// </summary>
/// <remarks>
/// <para>
/// WorkflowDefinitionBuilder implements a fluent API for building <see cref="WorkflowDefinitionFile"/> objects.
/// It is typically used in configuration or initialization code to define multi-step workflows programmatically,
/// without manual JSON manipulation or reflection.
/// </para>
/// <para>
/// Key responsibilities:
/// </para>
/// <list type="bullet">
/// <item>Track the workflow's unique key, display name, and version.</item>
/// <item>Collect state definitions using <see cref="AddState"/>.</item>
/// <item>Collect transitions between states using <see cref="AddTransition"/>.</item>
/// <item>Set the initial state and instance policy.</item>
/// <item>Return a fully-formed <see cref="WorkflowDefinitionFile"/> via <see cref="Build"/>.</item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// var workflow = new WorkflowDefinitionBuilder()
///     .Key("pension-application")
///     .DisplayName("Pension Application")
///     .Version(1)
///     .StartsAt("collect-details")
///     .InstancePolicy("single")
///     .AddState("collect-details", s => s
///         .DisplayName("Your Details")
///         .StepType("question")
///         .AddFieldset([
///             new FieldFile { FieldKey = "full-name", Label = "Full name", FieldType = "text", Required = true }
///         ]))
///     .AddState("check-answers", s => s
///         .DisplayName("Check Your Answers")
///         .StepType("check-answers"))
///     .AddState("submitted", s => s
///         .DisplayName("Application Submitted")
///         .StepType("confirmation"))
///     .AddTransition("collect-details", "check-answers", "continue")
///     .AddTransition("check-answers", "submitted", "submit")
///     .Build();
/// </code>
/// </example>
public class WorkflowDefinitionBuilder
{
    private string _definitionKey = "";
    private string _displayName = "";
    private int _version = 1;
    private string _initialState = "";
    private string _instancePolicy = "single";
    private readonly List<StepDefinition> _states = new();
    private readonly List<WorkflowTransitionFile> _transitions = new();

    /// <summary>
    /// Sets the unique key for this workflow definition.
    /// </summary>
    /// <param name="definitionKey">A unique, URL-safe identifier (e.g., "pension-application", "contact-form").</param>
    /// <returns>This builder instance for method chaining.</returns>
    public WorkflowDefinitionBuilder Key(string definitionKey)
    {
        _definitionKey = definitionKey;
        return this;
    }

    /// <summary>
    /// Sets the human-readable display name for this workflow.
    /// </summary>
    /// <param name="name">Display name (e.g., "Pension Application", "Contact Us Form").</param>
    /// <returns>This builder instance for method chaining.</returns>
    public WorkflowDefinitionBuilder DisplayName(string name)
    {
        _displayName = name;
        return this;
    }

    /// <summary>
    /// Sets the version number for this workflow definition.
    /// </summary>
    /// <param name="version">Version number (default: 1). Increment when changing definition behavior significantly.</param>
    /// <returns>This builder instance for method chaining.</returns>
    public WorkflowDefinitionBuilder Version(int version)
    {
        _version = version;
        return this;
    }

    /// <summary>
    /// Sets the key of the state where users enter the workflow.
    /// </summary>
    /// <param name="stateKey">The key of the state to start at (must be added via <see cref="AddState"/>).</param>
    /// <returns>This builder instance for method chaining.</returns>
    public WorkflowDefinitionBuilder StartsAt(string stateKey)
    {
        _initialState = stateKey;
        return this;
    }

    /// <summary>
    /// Sets the instance creation policy — controls whether users see existing instances or always create new ones.
    /// </summary>
    /// <param name="policy">One of: "single" (reuse existing instance), "multiple" (always create new), "prompt" (ask user if instance exists).</param>
    /// <returns>This builder instance for method chaining.</returns>
    public WorkflowDefinitionBuilder InstancePolicy(string policy)
    {
        _instancePolicy = policy;
        return this;
    }

    /// <summary>
    /// Adds a state to this workflow definition.
    /// </summary>
    /// <param name="stateKey">A unique identifier for this state (e.g., "collect-details", "check-answers").</param>
    /// <param name="configure">A lambda to configure the state (display name, step type, field groups, allowed actions).</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// The configure lambda receives a <see cref="WorkflowStateBuilder"/> that allows you to set the state's properties.
    /// Call this method once per state in your workflow.
    /// </remarks>
    public WorkflowDefinitionBuilder AddState(string stateKey, Action<WorkflowStateBuilder> configure)
    {
        var builder = new WorkflowStateBuilder(stateKey);
        configure(builder);
        _states.Add(builder.Build());
        return this;
    }

    /// <summary>
    /// Adds a transition (connection) from one state to another.
    /// </summary>
    /// <param name="fromState">The key of the state users are leaving.</param>
    /// <param name="toState">The key of the state users are entering.</param>
    /// <param name="action">The action name the user takes (e.g., "continue", "submit", "back").</param>
    /// <param name="requiresRole">Optional role name — if specified, only users with this role can take this transition.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// Transitions define the allowed paths through the workflow. 
    /// The Business App uses these to validate whether advancing from one state to another is allowed.
    /// </remarks>
    public WorkflowDefinitionBuilder AddTransition(string fromState, string toState, string action, string? requiresRole = null)
    {
        _transitions.Add(new WorkflowTransitionFile
        {
            FromState = fromState,
            ToState = toState,
            Action = action,
            RequiresRole = requiresRole
        });
        return this;
    }

    /// <summary>
    /// Builds and returns a complete workflow definition file.
    /// </summary>
    /// <returns>A <see cref="WorkflowDefinitionFile"/> with all states, transitions, and configuration.</returns>
    /// <remarks>
    /// This method is called after chaining all builder methods.
    /// Ensure all required properties (Key, DisplayName, InitialState) are set before calling Build().
    /// </remarks>
    public WorkflowDefinitionFile Build()
    {
        return new WorkflowDefinitionFile
        {
            DefinitionKey = _definitionKey,
            DisplayName = _displayName,
            Version = _version,
            InitialState = _initialState,
            InstancePolicy = _instancePolicy,
            States = _states,
            Transitions = _transitions
        };
    }
}

/// <summary>
/// Fluent builder for creating workflow states within a definition.
/// </summary>
/// <remarks>
/// <para>
/// WorkflowStateBuilder is used internally by <see cref="WorkflowDefinitionBuilder.AddState"/>.
/// It allows detailed configuration of individual workflow states, including their display name, step type,
/// associated field groups, and available actions.
/// </para>
/// <para>
/// Typical usage is via the lambda passed to AddState():
/// <code>
/// .AddState("collect-details", s => s
///     .DisplayName("Your Details")
///     .StepType("question")
///     .AddFieldset("personal-info", "address")
///     .AllowActions("continue", "back"))
/// </code>
/// </para>
/// </remarks>
public class WorkflowStateBuilder
{
    private readonly string _stateKey;
    private string _displayName = "";
    private readonly List<PrismComponentDefinition> _components = new();

    internal WorkflowStateBuilder(string stateKey)
    {
        _stateKey = stateKey;
    }

    /// <summary>
    /// Sets the human-readable display name for this state.
    /// </summary>
    /// <param name="name">Display name (e.g., "Your Details", "Check Your Answers").</param>
    /// <returns>This builder instance for method chaining.</returns>
    public WorkflowStateBuilder DisplayName(string name)
    {
        _displayName = name;
        return this;
    }

    /// <summary>Adds a fieldset component with inline fields.</summary>
    /// <param name="fields">The fields rendered by the fieldset.</param>
    /// <param name="legend">Optional legend text shown above the fieldset.</param>
    /// <param name="legendSize">Optional legend size: "xl" | "l" | "m" | "s".</param>
    /// <returns>This builder instance for method chaining.</returns>
    public WorkflowStateBuilder AddFieldset(IReadOnlyList<FieldFile> fields, string? legend = null, string? legendSize = null)
    {
        _components.Add(new PrismComponentDefinition { Type = "fieldset", Fields = fields, Legend = legend, LegendSize = legendSize });
        return this;
    }

    /// <summary>Adds a fieldset component referencing an existing field group.</summary>
    /// <param name="fieldGroupKey">Legacy key of the field group to render.</param>
    /// <param name="legend">Optional legend text overriding the field group DisplayName.</param>
    /// <param name="legendSize">Optional legend size: "xl" | "l" | "m" | "s".</param>
    /// <returns>This builder instance for method chaining.</returns>
    public WorkflowStateBuilder AddFieldset(string fieldGroupKey, string? legend = null, string? legendSize = null)
    {
        _components.Add(new PrismComponentDefinition { Type = "fieldset", FieldGroupKey = fieldGroupKey, Legend = legend, LegendSize = legendSize });
        return this;
    }

    /// <summary>Adds a summary-list (check-answers) component with inline fields.</summary>
    /// <param name="fields">The fields to summarise.</param>
    /// <param name="changeStateKey">The state key the "Change" links navigate to.</param>
    /// <param name="title">Optional heading shown above the summary list.</param>
    /// <returns>This builder instance for method chaining.</returns>
    public WorkflowStateBuilder AddSummaryList(IReadOnlyList<FieldFile> fields, string? changeStateKey = null, string? title = null)
    {
        _components.Add(new PrismComponentDefinition { Type = "summary-list", Fields = fields, ChangeStateKey = changeStateKey, Title = title });
        return this;
    }

    /// <summary>Adds a summary-list (check-answers) component referencing a field group.</summary>
    /// <param name="fieldGroupKey">The key of the field group to summarise.</param>
    /// <param name="changeStateKey">The state key the "Change" links navigate to.</param>
    /// <param name="title">Optional heading overriding the field group DisplayName.</param>
    /// <returns>This builder instance for method chaining.</returns>
    public WorkflowStateBuilder AddSummaryList(string fieldGroupKey, string? changeStateKey = null, string? title = null)
    {
        _components.Add(new PrismComponentDefinition { Type = "summary-list", FieldGroupKey = fieldGroupKey, ChangeStateKey = changeStateKey, Title = title });
        return this;
    }

    /// <summary>Adds a content component (body, heading, panel, inset-text, warning-text, details, notification-banner).</summary>
    /// <param name="type">The GDS component type.</param>
    /// <param name="content">The component body or paragraph text.</param>
    /// <param name="heading">Optional heading text.</param>
    /// <param name="bannerType">For notification-banner: "info" | "success" | "warning".</param>
    /// <param name="level">For heading components: heading level 1-6.</param>
    /// <returns>This builder instance for method chaining.</returns>
    public WorkflowStateBuilder AddContent(string type, string content, string? heading = null, string? bannerType = null, int? level = null)
    {
        _components.Add(new PrismComponentDefinition { Type = type, Content = content, Heading = heading, BannerType = bannerType, Level = level });
        return this;
    }

    /// <summary>Adds a generic component definition directly.</summary>
    /// <param name="component">The component definition to add.</param>
    /// <returns>This builder instance for method chaining.</returns>
    public WorkflowStateBuilder AddComponent(PrismComponentDefinition component)
    {
        _components.Add(component);
        return this;
    }

    /// <summary>
    /// Configures this state as a "waiting" step — shown when the workflow is paused
    /// pending external processing (e.g., payment provider, review queue, background job).
    /// </summary>
    /// <param name="message">
    /// The main message to display while waiting
    /// (e.g., "We're processing your payment. This usually takes 30 seconds.").
    /// </param>
    /// <param name="expectedWaitSeconds">
    /// Expected wait time in seconds, used to set user expectations
    /// (e.g., 30 → "This usually takes about 30 seconds.").
    /// </param>
    /// <param name="pollIntervalMs">
    /// How often the client should poll for a state change, in milliseconds (default: 3000).
    /// Lower values give a more responsive feel; higher values reduce server load.
    /// </param>
    /// <param name="allowDefer">
    /// Whether to show the "leave and come back later" option (default: true).
    /// When true, a link to the workflow hub is shown so users can return later.
    /// </param>
    /// <param name="deferMessage">
    /// Optional custom message for the defer option. If null, a sensible default is used.
    /// </param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Calling this method also sets the step type to <c>"waiting"</c> automatically —
    /// you do not need to call <see cref="StepType"/> separately.
    /// </para>
    /// <para>
    /// The waiting state renders an auto-polling UI that detects when the workflow advances
    /// (triggered by an external actor calling AdvanceAsync) and reloads the page automatically.
    /// </para>
    /// <example>
    /// <code>
    /// .AddState("processing-payment", s => s
    ///     .DisplayName("Processing Your Payment")
    ///     .WaitWith(
    ///         message: "We are processing your payment. Please do not close this page.",
    ///         expectedWaitSeconds: 30,
    ///         pollIntervalMs: 3000,
    ///         allowDefer: true,
    ///         deferMessage: "You can leave and return via My Applications when processing is complete."
    ///     ))
    /// </code>
    /// </example>
    /// </remarks>
    public WorkflowStateBuilder WaitWith(
        string message,
        int expectedWaitSeconds,
        int pollIntervalMs = 3000,
        bool allowDefer = true,
        string? deferMessage = null)
    {
        _components.Add(new PrismComponentDefinition
        {
            Type = "waiting",
            Content = message,
            ExpectedWaitSeconds = expectedWaitSeconds,
            PollIntervalMs = pollIntervalMs,
            AllowDefer = allowDefer,
            DeferMessage = deferMessage
        });
        return this;
    }

    internal StepDefinition Build()
    {
        return new StepDefinition
        {
            StateKey = _stateKey,
            DisplayName = _displayName,
            Components = _components
        };
    }
}
