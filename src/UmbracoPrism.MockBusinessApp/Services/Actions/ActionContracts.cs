using Wayfinder.Models.ServiceDesign;
using UmbracoPrism.MockBusinessApp.Services.Actions.ActionCatalog;
using UmbracoPrism.ProcessManager.Models;

namespace UmbracoPrism.MockBusinessApp.Services.Actions;

public interface IWorkflowActionHandler
{
    string ActionType { get; }

    Task<WorkflowActionExecutionResult> ExecuteAsync(
        ActionDefinition action,
        WorkflowActionExecutionContext context,
        CancellationToken cancellationToken);
}

public interface IWorkflowActionRegistry
{
    IReadOnlyList<ActionCatalogEntry> GetCatalog();

    IWorkflowActionHandler? Resolve(string actionType);
}

public sealed record WorkflowActionExecutionContext
{
    public required ServiceBlueprint Definition { get; init; }

    public required ServiceRequest Instance { get; init; }

    public StageDefinition? SourceState { get; init; }

    public required StageDefinition TargetState { get; init; }

    public RouteFile? Transition { get; init; }

    public string? TriggerAction { get; init; }

    public IReadOnlyDictionary<string, object?> FieldValues { get; init; } = new Dictionary<string, object?>();
}

public sealed record WorkflowActionExecutionResult
{
    public bool Succeeded { get; init; } = true;

    public string? Summary { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public IReadOnlyDictionary<string, object?> Outputs { get; init; } = new Dictionary<string, object?>();

    public static WorkflowActionExecutionResult Success(
        string? summary = null,
        IReadOnlyDictionary<string, object?>? outputs = null) =>
        new()
        {
            Summary = summary,
            Outputs = outputs ?? new Dictionary<string, object?>()
        };

    public static WorkflowActionExecutionResult Failure(string errorCode, string errorMessage) =>
        new()
        {
            Succeeded = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
}
