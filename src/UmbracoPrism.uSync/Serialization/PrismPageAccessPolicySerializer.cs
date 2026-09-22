using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Persistence;
using uSync.Core;
using uSync.Core.Models;
using uSync.Core.Serialization;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.uSync.Serialization;

/// <summary>
/// Deliberately never serializes ContentKey — it's a per-database GUID, meaningless in a
/// different environment (this repo's own content isn't uSync-portable at all; pages are
/// created by C# seeders, so the same seeder run against two databases produces two different
/// GUIDs for "the same" page — see the design discussion this class implements). ContentRoute is
/// the portable reference instead: on import, it's resolved against the TARGET environment's own
/// content tree via IDocumentUrlService.GetDocumentKeyByRoute (the same API
/// PrismPageAccessFilter/_DesktopNav.cshtml already use for the opposite direction) to find that
/// environment's own real ContentKey. If the route doesn't resolve there — the page hasn't been
/// deployed/published yet — the import FAILS loudly rather than silently creating a
/// dangling/unenforceable policy or skipping it outright and leaving the page unprotected: the
/// same "never fail open" principle applied everywhere else in this feature.
/// </summary>
[SyncSerializer("7c9e2a48-3d61-4f2b-8a75-1e6c4d9b2f83", "Prism Page Access Policy Serializer", "PrismPageAccessPolicy")]
public class PrismPageAccessPolicySerializer : SyncSerializerRoot<PrismPageAccessPolicySchema>, ISyncSerializer<PrismPageAccessPolicySchema>
{
    private readonly IUmbracoDatabaseFactory _databaseFactory;
    private readonly IDocumentUrlService _documentUrlService;
    private readonly IPrismPageAccessResolver _pageAccessResolver;

    public PrismPageAccessPolicySerializer(
        ILogger<SyncSerializerRoot<PrismPageAccessPolicySchema>> logger,
        IUmbracoDatabaseFactory databaseFactory,
        IDocumentUrlService documentUrlService,
        IPrismPageAccessResolver pageAccessResolver) : base(logger)
    {
        _databaseFactory = databaseFactory;
        _documentUrlService = documentUrlService;
        _pageAccessResolver = pageAccessResolver;
    }

    public override Guid ItemKey(PrismPageAccessPolicySchema item) => DeterministicGuid(NormalizeRoute(item.ContentRoute));
    public override string ItemAlias(PrismPageAccessPolicySchema item) => NormalizeRoute(item.ContentRoute);

    public override Task<PrismPageAccessPolicySchema?> FindItemAsync(Guid key)
    {
        using var db = _databaseFactory.CreateDatabase();
        var result = db.Fetch<PrismPageAccessPolicySchema>()
            .FirstOrDefault(p => DeterministicGuid(NormalizeRoute(p.ContentRoute)) == key);
        return Task.FromResult(result);
    }

    public override Task<PrismPageAccessPolicySchema?> FindItemAsync(string alias)
    {
        using var db = _databaseFactory.CreateDatabase();
        var result = db.Fetch<PrismPageAccessPolicySchema>()
            .FirstOrDefault(p => NormalizeRoute(p.ContentRoute) == alias);
        return Task.FromResult(result);
    }

    public override Task SaveItemAsync(PrismPageAccessPolicySchema item)
    {
        using var db = _databaseFactory.CreateDatabase();
        if (item.Id > 0)
            db.Update(item);
        else
            db.Insert(item);
        _pageAccessResolver.Invalidate("usync-import");
        return Task.CompletedTask;
    }

    public override Task DeleteItemAsync(PrismPageAccessPolicySchema item)
    {
        using var db = _databaseFactory.CreateDatabase();
        db.Delete(item);
        _pageAccessResolver.Invalidate("usync-delete");
        return Task.CompletedTask;
    }

    protected override Task<SyncAttempt<XElement>> SerializeCoreAsync(PrismPageAccessPolicySchema item, SyncSerializerOptions options)
    {
        if (item is null)
            return Task.FromResult(SyncAttempt<XElement>.Fail(string.Empty, null, ChangeType.Fail, "Item is null", null));

        if (string.IsNullOrWhiteSpace(item.ContentRoute))
        {
            return Task.FromResult(SyncAttempt<XElement>.Fail(
                $"policy-{item.Id}", null, ChangeType.Fail,
                "Cannot export a page-access policy whose page has never been published — no route to export by. Publish the page first.",
                null));
        }

        var alias = ItemAlias(item);
        var allowedTenantNames = ParseAllowList(item.TenantAllowListJson);

        var node = InitializeBaseNode(item, alias, 1);
        node.Add(
            new XElement("Info",
                new XElement("ContentRoute", item.ContentRoute),
                new XElement("RequiresSignIn", item.RequiresSignIn)),
            new XElement("TenantAllowList",
                allowedTenantNames.Select(name => new XElement("Tenant", name))));

        return Task.FromResult(SyncAttempt<XElement>.Succeed(alias, node, ChangeType.Export, new List<uSyncChange>()));
    }

    protected override async Task<SyncAttempt<PrismPageAccessPolicySchema>> DeserializeCoreAsync(XElement node, SyncSerializerOptions options)
    {
        var existing = await FindItemAsync(node);
        var schema = existing ?? new PrismPageAccessPolicySchema();

        var info = node.Element("Info");
        var route = info?.Element("ContentRoute")?.Value;

        if (string.IsNullOrWhiteSpace(route))
            return SyncAttempt<PrismPageAccessPolicySchema>.Fail(node.GetAlias(), default, ChangeType.Fail,
                "Page-access policy has no ContentRoute to import by", null);

        // The whole point of this resolution: ContentKey from the SOURCE environment is
        // meaningless here. Find (or refuse to find) the equivalent page in THIS environment by
        // its route instead.
        var resolvedKey = _documentUrlService.GetDocumentKeyByRoute(route, culture: null, documentStartNodeId: null, isDraft: false);
        if (resolvedKey is null)
        {
            return SyncAttempt<PrismPageAccessPolicySchema>.Fail(node.GetAlias(), default, ChangeType.Fail,
                $"No published page was found at route '{route}' in this environment — deploy/publish that page first, then re-run this sync. Refusing to import a page-access policy that can't be bound to a real page.",
                null);
        }

        schema.ContentKey = resolvedKey.Value;
        schema.ContentRoute = route;
        schema.RequiresSignIn = bool.TryParse(info?.Element("RequiresSignIn")?.Value, out var requiresSignIn) && requiresSignIn;

        var allowedTenantNames = node.Element("TenantAllowList")?
            .Elements("Tenant")
            .Select(e => e.Value)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray() ?? [];

        schema.TenantAllowListJson = allowedTenantNames.Length > 0
            ? JsonSerializer.Serialize(allowedTenantNames)
            : null;

        return SyncAttempt<PrismPageAccessPolicySchema>.Succeed(ItemAlias(schema), schema, ChangeType.Import, new List<uSyncChange>());
    }

    private static Guid DeterministicGuid(string slug)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes($"prism-page-access:{slug}"));
        return new Guid(hash);
    }

    private static string NormalizeRoute(string? route)
    {
        if (string.IsNullOrWhiteSpace(route)) return "unrouted";
        return route.Trim().ToLowerInvariant();
    }

    private static string[] ParseAllowList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<string[]>(json) ?? []; }
        catch { return []; }
    }
}
