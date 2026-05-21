namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Summary of one authored workflow document as exposed by an <see cref="IAuthoredWorkflowStore"/>.
/// <see cref="WorkflowKey"/> is the host-facing lookup key and may differ from <see cref="DefinitionKey"/>.
/// </summary>
public sealed record AuthoredWorkflowStoreEntry
{
    public required string WorkflowKey { get; init; }

    public Guid? Id { get; init; }

    public string? DefinitionKey { get; init; }

    public string? DisplayName { get; init; }

    public bool IsLoadable { get; init; }

    public string? ErrorMessage { get; init; }
}
