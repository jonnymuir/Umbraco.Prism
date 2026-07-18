using System.Text.Json;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.WorkflowRuntime.Abstractions;

namespace UmbracoPrism.Core.Services.Workflow;

/// <summary>
/// Boot-time <see cref="IWorkflowDefinitionStore"/> that seeds <c>CmsWorkflowEngine</c> from the
/// prismCmsWorkflowDefinition table at startup. Deliberately has no dependency on
/// <c>IWorkflowRuntimeEngine</c> — unlike <see cref="UmbracoCmsWorkflowDefinitionStore"/> (the
/// authoring-side store), which pushes saves back into the live engine and therefore must depend
/// on it. Depending on the engine here would create a DI cycle at construction time.
/// </summary>
public sealed class UmbracoCmsWorkflowDefinitionBootStore(IUmbracoDatabaseFactory databaseFactory)
    : IWorkflowDefinitionStore
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowOutOfOrderMetadataProperties = true
    };

    public IReadOnlyDictionary<string, WorkflowDefinitionFile> LoadDefinitions(ILogger logger)
    {
        using var db = databaseFactory.CreateDatabase();
        var rows = db.Fetch<PrismCmsWorkflowDefinitionSchema>("SELECT * FROM prismCmsWorkflowDefinition");

        var definitions = new Dictionary<string, WorkflowDefinitionFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            try
            {
                var workflow = JsonSerializer.Deserialize<WorkflowDefinitionFile>(row.Json, ReadOptions);
                if (workflow is not null)
                {
                    definitions[row.DefinitionKey] = workflow with { Version = row.Version };
                }
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Failed to deserialize CMS Workflow definition '{Key}' at boot; skipping.", row.DefinitionKey);
            }
        }

        logger.LogInformation("CMS Workflow boot store loaded {Count} definition(s) from the database.", definitions.Count);
        return definitions;
    }
}
