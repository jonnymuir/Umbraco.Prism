namespace UmbracoPrism.Core.Exceptions;

/// <summary>
/// Exception thrown when optimistic concurrency check fails during workflow state transitions.
/// </summary>
public class OptimisticConcurrencyException : Exception
{
    /// <summary>
    /// Gets the workflow instance identifier.
    /// </summary>
    public string InstanceId { get; }

    /// <summary>
    /// Gets the expected state version.
    /// </summary>
    public int ExpectedVersion { get; }

    /// <summary>
    /// Gets the actual current state version.
    /// </summary>
    public int ActualVersion { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OptimisticConcurrencyException"/> class.
    /// </summary>
    /// <param name="instanceId">The workflow instance identifier.</param>
    /// <param name="expected">The expected state version.</param>
    /// <param name="actual">The actual current state version.</param>
    public OptimisticConcurrencyException(string instanceId, int expected, int actual)
        : base($"Workflow instance {instanceId} has been modified. Expected version {expected}, but current version is {actual}.")
    {
        InstanceId = instanceId;
        ExpectedVersion = expected;
        ActualVersion = actual;
    }
}

/// <summary>
/// Exception thrown when a workflow instance cannot be found.
/// </summary>
public class WorkflowInstanceNotFoundException : Exception
{
    /// <summary>
    /// Gets the workflow instance identifier.
    /// </summary>
    public string InstanceId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowInstanceNotFoundException"/> class.
    /// </summary>
    /// <param name="instanceId">The workflow instance identifier.</param>
    public WorkflowInstanceNotFoundException(string instanceId)
        : base($"Workflow instance {instanceId} was not found.")
    {
        InstanceId = instanceId;
    }
}

/// <summary>
/// Exception thrown when a user attempts to access a workflow instance that does not belong to their tenant.
/// </summary>
public class UnauthorizedWorkflowAccessException : Exception
{
    /// <summary>
    /// Gets the workflow instance identifier.
    /// </summary>
    public string InstanceId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnauthorizedWorkflowAccessException"/> class.
    /// </summary>
    /// <param name="instanceId">The workflow instance identifier.</param>
    public UnauthorizedWorkflowAccessException(string instanceId)
        : base($"Access denied to workflow instance {instanceId}. The instance does not belong to your tenant.")
    {
        InstanceId = instanceId;
    }
}

/// <summary>
/// Exception thrown when an invalid state transition is attempted.
/// </summary>
public class InvalidWorkflowTransitionException : Exception
{
    /// <summary>
    /// Gets the source state.
    /// </summary>
    public string FromState { get; }

    /// <summary>
    /// Gets the action that was attempted.
    /// </summary>
    public string Action { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidWorkflowTransitionException"/> class.
    /// </summary>
    /// <param name="fromState">The source state.</param>
    /// <param name="action">The action that was attempted.</param>
    public InvalidWorkflowTransitionException(string fromState, string action)
        : base($"Invalid transition: No transition exists from state '{fromState}' with action '{action}'.")
    {
        FromState = fromState;
        Action = action;
    }
}
