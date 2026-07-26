using System.Reflection;
using ModelContextProtocol.Server;

namespace UmbracoPrism.ProcessManager.Mcp;

/// <summary>
/// MCP resources exposing the service blueprint authoring reference docs — the calculation
/// expression grammar, the full ServiceBlueprint contract, and the general
/// service design principles a blueprint should be judged against — so an agent
/// connected only over MCP (no repo checkout) can fetch them directly. Content is
/// embedded from the canonical, tool-agnostic markdown in docs/guides/ at build
/// time; there is no separate copy to keep in sync.
/// </summary>
[McpServerResourceType]
public static class ServiceBlueprintAuthoringResources
{
    [McpServerResource(
        Name = "calculation-language",
        UriTemplate = "service-blueprint-docs://calculation-language",
        Title = "Prism Calculation Expression Language",
        MimeType = "text/markdown")]
    [System.ComponentModel.Description(
        "Grammar, function reference (if/min/max/clamp/abs/floor/round/pow/lookup), tables/series " +
        "semantics, decimal numeric semantics, showWhen visibility expressions, and a worked walkthrough " +
        "for the declarative calculation expression language used in a blueprint's `calculations` block " +
        "and any component's `showWhen`.")]
    public static string CalculationLanguage() => ReadEmbeddedDoc("calculation-language.md");

    [McpServerResource(
        Name = "authoring-guide",
        UriTemplate = "service-blueprint-docs://authoring-guide",
        Title = "Prism Blueprint Authoring Contract",
        MimeType = "text/markdown")]
    [System.ComponentModel.Description(
        "The full ServiceBlueprint JSON contract: touchpoints, routes, gateways and the gateway " +
        "routing rule, queues, the component catalog, response touchpoints, and the save/conflict protocol.")]
    public static string AuthoringGuide() => ReadEmbeddedDoc("reference-workflow-contract.md");

    [McpServerResource(
        Name = "service-design-principles",
        UriTemplate = "service-blueprint-docs://service-design-principles",
        Title = "Service Design Principles",
        MimeType = "text/markdown")]
    [System.ComponentModel.Description(
        "Industry-agnostic service design grounding for whoever is authoring a blueprint: the Design " +
        "Council Double Diamond process, the GOV.UK Service Standard, and Lou Downe's 15 principles of " +
        "good services, each mapped to concrete service-blueprint-authoring decisions. Does not cover sector-specific " +
        "regulation or domain best practice — bring that yourself alongside this resource.")]
    public static string ServiceDesignPrinciples() => ReadEmbeddedDoc("service-design-principles.md");

    [McpServerResource(
        Name = "ai-service-blueprint-authoring",
        UriTemplate = "service-blueprint-docs://ai-service-blueprint-authoring",
        Title = "AI-Ready Blueprint Authoring — Integrator Guide",
        MimeType = "text/markdown")]
    [System.ComponentModel.Description(
        "How a host app wires this MCP surface into its own pipeline (MapPrismServiceBlueprintAuthoringMcp, " +
        "IServiceBlueprintSourceStore, auth). For an integrator setting this up, not for an agent authoring a " +
        "blueprint — that agent wants authoring-guide, calculation-language, or service-design-principles " +
        "instead.")]
    public static string AiServiceBlueprintAuthoring() => ReadEmbeddedDoc("ai-service-blueprint-authoring.md");

    private static string ReadEmbeddedDoc(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"{typeof(ServiceBlueprintAuthoringResources).Namespace}.docs.{fileName}";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded doc '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
