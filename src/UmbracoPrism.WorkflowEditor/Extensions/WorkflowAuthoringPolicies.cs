namespace UmbracoPrism.WorkflowEditor.Extensions;

/// <summary>
/// Authorization policy names exposed by the workflow-editor endpoint group.
/// Hosts are expected to register a policy matching <see cref="WorkflowAuthor"/>
/// before calling <see cref="WorkflowEditorEndpointExtensions.MapPrismWorkflowEditor"/>.
/// </summary>
public static class WorkflowAuthoringPolicies
{
    /// <summary>
    /// Required on every <c>/api/workflow-authoring/*</c> route. Hosts decide the exact
    /// requirements (typically <c>RequireAuthenticatedUser</c> plus tenant claims);
    /// the editor group asserts only that the principal has been admitted by this policy.
    /// </summary>
    public const string WorkflowAuthor = "WorkflowAuthor";
}
