using System.Reflection;
using ModelContextProtocol.Server;

namespace UmbracoPrism.WorkflowRuntime.Mcp;

/// <summary>
/// MCP resources exposing the workflow authoring reference docs — the calculation
/// expression grammar and the full WorkflowDefinitionFile contract — so an agent
/// connected only over MCP (no repo checkout) can fetch them directly. Content is
/// embedded from the canonical, tool-agnostic markdown in docs/guides/ at build
/// time; there is no separate copy to keep in sync.
/// </summary>
[McpServerResourceType]
public static class WorkflowAuthoringResources
{
    [McpServerResource(
        Name = "calculation-language",
        UriTemplate = "workflow-docs://calculation-language",
        Title = "Prism Calculation Expression Language",
        MimeType = "text/markdown")]
    [System.ComponentModel.Description(
        "Grammar, function reference (if/min/max/clamp/abs/floor/round/pow/lookup), tables/series " +
        "semantics, decimal numeric semantics, showWhen visibility expressions, and a worked walkthrough " +
        "for the declarative calculation expression language used in a workflow's `calculations` block " +
        "and any component's `showWhen`.")]
    public static string CalculationLanguage() => ReadEmbeddedDoc("calculation-language.md");

    [McpServerResource(
        Name = "authoring-guide",
        UriTemplate = "workflow-docs://authoring-guide",
        Title = "Prism Workflow Authoring Contract",
        MimeType = "text/markdown")]
    [System.ComponentModel.Description(
        "The full WorkflowDefinitionFile JSON contract: states, routes, gateways and the gateway " +
        "routing rule, queues, the component catalog, response states, and the save/conflict protocol.")]
    public static string AuthoringGuide() => ReadEmbeddedDoc("reference-workflow-contract.md");

    private static string ReadEmbeddedDoc(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"{typeof(WorkflowAuthoringResources).Namespace}.docs.{fileName}";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded doc '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
