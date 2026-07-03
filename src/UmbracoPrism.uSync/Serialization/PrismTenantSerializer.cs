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
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.uSync.Serialization;

[SyncSerializer("3f7b9c21-4e8d-4a1c-b6d2-5e0f1a2b3c4d", "Prism Tenant Serializer", "PrismTenant")]
public class PrismTenantSerializer : SyncSerializerRoot<PrismTenantSchema>, ISyncSerializer<PrismTenantSchema>
{
    private readonly IUmbracoDatabaseFactory _databaseFactory;
    private readonly ITenantTokenResolver _tokenResolver;
    private readonly ITenantService _tenantService;

    public PrismTenantSerializer(
        ILogger<SyncSerializerRoot<PrismTenantSchema>> logger,
        IUmbracoDatabaseFactory databaseFactory,
        ITenantTokenResolver tokenResolver,
        ITenantService tenantService) : base(logger)
    {
        _databaseFactory = databaseFactory;
        _tokenResolver = tokenResolver;
        _tenantService = tenantService;
    }

    public override Guid ItemKey(PrismTenantSchema item) => DeterministicGuid(Slugify(item.Name));
    public override string ItemAlias(PrismTenantSchema item) => Slugify(item.Name);

    public override Task<PrismTenantSchema?> FindItemAsync(Guid key)
    {
        using var db = _databaseFactory.CreateDatabase();
        var result = db.Fetch<PrismTenantSchema>()
            .FirstOrDefault(t => DeterministicGuid(Slugify(t.Name)) == key);
        return Task.FromResult(result);
    }

    public override Task<PrismTenantSchema?> FindItemAsync(string alias)
    {
        using var db = _databaseFactory.CreateDatabase();
        var result = db.Fetch<PrismTenantSchema>()
            .FirstOrDefault(t => Slugify(t.Name) == alias);
        return Task.FromResult(result);
    }

    public override Task SaveItemAsync(PrismTenantSchema item)
    {
        using var db = _databaseFactory.CreateDatabase();
        if (item.Id > 0)
            db.Update(item);
        else
            db.Insert(item);
        _tenantService.InvalidateDomain(item.Hostname, "usync-import");
        return Task.CompletedTask;
    }

    public override Task DeleteItemAsync(PrismTenantSchema item)
    {
        using var db = _databaseFactory.CreateDatabase();
        db.Delete(item);
        _tenantService.InvalidateDomain(item.Hostname, "usync-delete");
        return Task.CompletedTask;
    }

    protected override Task<SyncAttempt<XElement>> SerializeCoreAsync(PrismTenantSchema item, SyncSerializerOptions options)
    {
        if (item is null)
            return Task.FromResult(SyncAttempt<XElement>.Fail(string.Empty, null, ChangeType.Fail, "Item is null", null));

        var alias = ItemAlias(item);
        var branding = ParseOverrides(item.BrandingOverrides);
        var mobileBranding = ParseOverrides(item.MobileBrandingOverrides);

        var node = InitializeBaseNode(item, alias, 1);
        node.Add(
            new XElement("Info",
                new XElement("Name", item.Name),
                new XElement("Hostname", item.Hostname),
                new XElement("AllowBiometricLogin", item.AllowBiometricLogin)),
            new XElement("Identity",
                NullableElement("EntraTenantId", item.EntraTenantId),
                NullableElement("EntraClientId", item.EntraClientId),
                NullableElement("SecretKeyName", item.SecretKeyName),
                NullableElement("OidcAuthority", item.OidcAuthority),
                NullableElement("OidcClientId", item.OidcClientId),
                NullableElement("OidcClientSecretProvider", item.OidcClientSecretProvider),
                NullableElement("OidcClientSecretReference", item.OidcClientSecretReference)),
            new XElement("Branding",
                branding.Select(kv => new XElement("Override", new XAttribute("Variable", kv.Key), kv.Value))),
            new XElement("MobileBranding",
                mobileBranding.Select(kv => new XElement("Override", new XAttribute("Variable", kv.Key), kv.Value))));

        return Task.FromResult(SyncAttempt<XElement>.Succeed(alias, node, ChangeType.Export, new List<uSyncChange>()));
    }

    protected override async Task<SyncAttempt<PrismTenantSchema>> DeserializeCoreAsync(XElement node, SyncSerializerOptions options)
    {
        var existing = await FindItemAsync(node);
        var schema = existing ?? new PrismTenantSchema();

        var info = node.Element("Info");
        var identity = node.Element("Identity");

        var rawHostname = info?.Element("Hostname")?.Value ?? string.Empty;
        schema.Hostname = (_tokenResolver.Resolve(rawHostname) ?? rawHostname).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(schema.Hostname))
            return SyncAttempt<PrismTenantSchema>.Fail(node.GetAlias(), default, ChangeType.Fail,
                "Hostname resolved to empty — check token configuration", null);

        schema.Name = info?.Element("Name")?.Value ?? string.Empty;
        schema.AllowBiometricLogin = ParseBool(info?.Element("AllowBiometricLogin")?.Value, defaultValue: true);
        schema.EntraTenantId = NullIfEmpty(identity?.Element("EntraTenantId")?.Value);
        schema.EntraClientId = NullIfEmpty(identity?.Element("EntraClientId")?.Value);
        schema.SecretKeyName = NullIfEmpty(identity?.Element("SecretKeyName")?.Value);
        schema.OidcAuthority = NullIfEmpty(identity?.Element("OidcAuthority")?.Value);
        schema.OidcClientId = NullIfEmpty(identity?.Element("OidcClientId")?.Value);
        schema.OidcClientSecretProvider = NullIfEmpty(identity?.Element("OidcClientSecretProvider")?.Value);
        schema.OidcClientSecretReference = NullIfEmpty(identity?.Element("OidcClientSecretReference")?.Value);

        var branding = node.Element("Branding")?
            .Elements("Override")
            .Where(e => e.Attribute("Variable") is not null)
            .ToDictionary(e => e.Attribute("Variable")!.Value, e => e.Value)
            ?? new Dictionary<string, string>();

        var mobileBranding = node.Element("MobileBranding")?
            .Elements("Override")
            .Where(e => e.Attribute("Variable") is not null)
            .ToDictionary(e => e.Attribute("Variable")!.Value, e => e.Value)
            ?? new Dictionary<string, string>();

        schema.BrandingOverrides = branding.Count > 0 ? JsonSerializer.Serialize(branding) : null;
        schema.MobileBrandingOverrides = mobileBranding.Count > 0 ? JsonSerializer.Serialize(mobileBranding) : null;

        // Preserve legacy inline secret — never exported, must survive a round-trip.
        if (existing is not null)
            schema.OidcClientSecret = existing.OidcClientSecret;

        return SyncAttempt<PrismTenantSchema>.Succeed(ItemAlias(schema), schema, ChangeType.Import, new List<uSyncChange>());
    }

    private static Guid DeterministicGuid(string slug)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes($"prism-tenant:{slug}"));
        return new Guid(hash);
    }

    private static string Slugify(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "unnamed-tenant";
        var sb = new StringBuilder();
        var prevHyphen = true;
        foreach (var ch in name.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch)) { sb.Append(ch); prevHyphen = false; }
            else if (!prevHyphen) { sb.Append('-'); prevHyphen = true; }
        }
        if (sb.Length > 0 && sb[^1] == '-') sb.Length--;
        return sb.Length > 0 ? sb.ToString() : "unnamed-tenant";
    }

    private static XElement? NullableElement(string name, string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : new XElement(name, value);

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static bool ParseBool(string? value, bool defaultValue) =>
        bool.TryParse(value, out var result) ? result : defaultValue;

    private static Dictionary<string, string> ParseOverrides(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new(); }
        catch { return new(); }
    }
}
