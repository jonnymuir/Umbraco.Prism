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
///         .WithFieldGroups("personal-info"))
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
///     .WithFieldGroups("personal-info", "address")
///     .AllowActions("continue", "back"))
/// </code>
/// </para>
/// </remarks>
public class WorkflowStateBuilder
{
    private readonly string _stateKey;
    private string _displayName = "";
    private string _stepType = "question";
    private readonly List<string> _allowedActions = new();
    private readonly List<string> _fieldGroupKeys = new();

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

    /// <summary>
    /// Sets the step type, which drives partial view selection and UI rendering.
    /// </summary>
    /// <param name="stepType">One of: "question" (form), "check-answers" (summary), "confirmation" (done), "status-timeline", "task-list".</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// The step type is not Archetype. It is the classification used by the front-end to decide which view partial to render.
    /// </remarks>
    public WorkflowStateBuilder StepType(string stepType)
    {
        _stepType = stepType;
        return this;
    }

    /// <summary>
    /// Specifies which actions users can take from this state.
    /// </summary>
    /// <param name="actions">Action names (e.g., "continue", "submit", "back").</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// These actions define which buttons or links are shown to the user at this step.
    /// </remarks>
    public WorkflowStateBuilder AllowActions(params string[] actions)
    {
        _allowedActions.AddRange(actions);
        return this;
    }

    /// <summary>
    /// Associates field groups with this state.
    /// </summary>
    /// <param name="groupKeys">One or more field group keys to display at this step (e.g., "personal-info", "address").</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// Field groups are rendered in the order specified. Use for question steps; summary and confirmation steps typically have no groups.
    /// </remarks>
    public WorkflowStateBuilder WithFieldGroups(params string[] groupKeys)
    {
        _fieldGroupKeys.AddRange(groupKeys);
        return this;
    }

    internal StepDefinition Build()
    {
        return new StepDefinition
        {
            StateKey = _stateKey,
            DisplayName = _displayName,
            StepType = _stepType,
            AllowedActions = _allowedActions,
            FieldGroupKeys = _fieldGroupKeys
        };
    }
}
