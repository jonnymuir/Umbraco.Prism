using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Infrastructure.Persistence;
using uSync.Core;
using uSync.Core.Models;
using uSync.Core.Serialization;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.WorkflowRuntime.Abstractions;

namespace UmbracoPrism.uSync.Serialization;

/// <summary>
/// Serializes CMS Workflow definitions to/from uSync's XML export format — mirrors
/// <see cref="PrismTenantSerializer"/>'s shape. A successful import also pushes the
/// definition into the live engine, the same promise a backoffice save already makes
/// (see <c>UmbracoCmsWorkflowDefinitionStore</c>), so an import into a running site takes
/// effect immediately rather than requiring a restart.
/// </summary>
[SyncSerializer("7a2e4f18-9c3b-4d67-a1e5-8f6b2c9d4a71", "Prism CMS Workflow Serializer", "PrismCmsWorkflow")]
public class PrismCmsWorkflowSerializer : SyncSerializerRoot<PrismCmsWorkflowDefinitionSchema>, ISyncSerializer<PrismCmsWorkflowDefinitionSchema>
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowOutOfOrderMetadataProperties = true
    };

    private readonly IUmbracoDatabaseFactory _databaseFactory;
    private readonly IWorkflowRuntimeEngine _engine;

    public PrismCmsWorkflowSerializer(
        ILogger<SyncSerializerRoot<PrismCmsWorkflowDefinitionSchema>> logger,
        IUmbracoDatabaseFactory databaseFactory,
        IWorkflowRuntimeEngine engine) : base(logger)
    {
        _databaseFactory = databaseFactory;
        _engine = engine;
    }

    public override Guid ItemKey(PrismCmsWorkflowDefinitionSchema item) => DeterministicGuid(item.DefinitionKey);
    public override string ItemAlias(PrismCmsWorkflowDefinitionSchema item) => item.DefinitionKey;

    public override Task<PrismCmsWorkflowDefinitionSchema?> FindItemAsync(Guid key)
    {
        using var db = _databaseFactory.CreateDatabase();
        var result = db.Fetch<PrismCmsWorkflowDefinitionSchema>()
            .FirstOrDefault(w => DeterministicGuid(w.DefinitionKey) == key);
        return Task.FromResult(result);
    }

    public override Task<PrismCmsWorkflowDefinitionSchema?> FindItemAsync(string alias)
    {
        using var db = _databaseFactory.CreateDatabase();
        var result = db.Fetch<PrismCmsWorkflowDefinitionSchema>()
            .FirstOrDefault(w => w.DefinitionKey == alias);
        return Task.FromResult(result);
    }

    public override Task SaveItemAsync(PrismCmsWorkflowDefinitionSchema item)
    {
        using var db = _databaseFactory.CreateDatabase();
        if (item.Id > 0)
            db.Update(item);
        else
            db.Insert(item);

        var workflow = JsonSerializer.Deserialize<WorkflowDefinitionFile>(item.Json, ReadOptions);
        if (workflow is not null)
        {
            _engine.UpdateDefinition(item.DefinitionKey, workflow with { Version = item.Version });
        }

        return Task.CompletedTask;
    }

    public override Task DeleteItemAsync(PrismCmsWorkflowDefinitionSchema item)
    {
        using var db = _databaseFactory.CreateDatabase();
        db.Delete(item);
        return Task.CompletedTask;
    }

    protected override Task<SyncAttempt<XElement>> SerializeCoreAsync(PrismCmsWorkflowDefinitionSchema item, SyncSerializerOptions options)
    {
        if (item is null)
            return Task.FromResult(SyncAttempt<XElement>.Fail(string.Empty, null, ChangeType.Fail, "Item is null", null));

        var alias = ItemAlias(item);
        var node = InitializeBaseNode(item, alias, 1);
        node.Add(
            new XElement("Info",
                new XElement("DefinitionKey", item.DefinitionKey),
                new XElement("DisplayName", item.DisplayName),
                new XElement("Version", item.Version)),
            new XElement("Definition", new XCData(item.Json)));

        return Task.FromResult(SyncAttempt<XElement>.Succeed(alias, node, ChangeType.Export, new List<uSyncChange>()));
    }

    protected override async Task<SyncAttempt<PrismCmsWorkflowDefinitionSchema>> DeserializeCoreAsync(XElement node, SyncSerializerOptions options)
    {
        var existing = await FindItemAsync(node);
        var schema = existing ?? new PrismCmsWorkflowDefinitionSchema();

        var info = node.Element("Info");
        schema.DefinitionKey = info?.Element("DefinitionKey")?.Value ?? string.Empty;

        if (string.IsNullOrWhiteSpace(schema.DefinitionKey))
            return SyncAttempt<PrismCmsWorkflowDefinitionSchema>.Fail(node.GetAlias(), default, ChangeType.Fail,
                "DefinitionKey is empty — check the exported file", null);

        schema.DisplayName = info?.Element("DisplayName")?.Value ?? string.Empty;
        schema.Version = int.TryParse(info?.Element("Version")?.Value, out var version) ? version : 1;
        schema.Json = node.Element("Definition")?.Value ?? string.Empty;
        schema.UpdatedUtc = DateTime.UtcNow;

        return SyncAttempt<PrismCmsWorkflowDefinitionSchema>.Succeed(ItemAlias(schema), schema, ChangeType.Import, new List<uSyncChange>());
    }

    private static Guid DeterministicGuid(string definitionKey)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes($"prism-cms-workflow:{definitionKey}"));
        return new Guid(hash);
    }
}
