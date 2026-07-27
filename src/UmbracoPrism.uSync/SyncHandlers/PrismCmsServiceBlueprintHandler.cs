using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Persistence;
using uSync.BackOffice;
using uSync.BackOffice.Configuration;
using uSync.BackOffice.Services;
using uSync.BackOffice.SyncHandlers;
using uSync.BackOffice.SyncHandlers.Interfaces;
using uSync.BackOffice.SyncHandlers.Models;
using BackOfficeConsts = global::uSync.BackOffice.uSyncConstants;
using ISyncItemFactory = global::uSync.Core.ISyncItemFactory;
using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.uSync.SyncHandlers;

/// <summary>
/// uSync handler for backoffice-authored CMS Service Blueprint definitions — mirrors
/// <see cref="PrismTenantHandler"/> exactly, giving CMS Service Blueprint definitions the same
/// export/import portability Tenants already have.
/// </summary>
[SyncHandler("PrismCmsServiceBlueprintHandler", "Prism CMS Service Blueprints", "CmsServiceBlueprints",
    BackOfficeConsts.Priorites.USYNC_RESERVED_UPPER + 101,
    Icon = "icon-diagram",
    EntityType = "prismCmsServiceBlueprint")]
public class PrismCmsServiceBlueprintHandler : SyncHandlerRoot<PrismCmsServiceBlueprintSchema, PrismCmsServiceBlueprintSchema>, ISyncHandler
{
    private readonly IUmbracoDatabaseFactory _databaseFactory;

    public override string Group => BackOfficeConsts.Groups.Settings;

    public PrismCmsServiceBlueprintHandler(
        ILogger<SyncHandlerRoot<PrismCmsServiceBlueprintSchema, PrismCmsServiceBlueprintSchema>> logger,
        AppCaches appCaches,
        IShortStringHelper shortStringHelper,
        ISyncFileService syncFileService,
        ISyncEventService mutexService,
        ISyncConfigService uSyncConfig,
        ISyncItemFactory itemFactory,
        IUmbracoDatabaseFactory databaseFactory)
        : base(logger, appCaches, shortStringHelper, syncFileService, mutexService, uSyncConfig, itemFactory)
    {
        _databaseFactory = databaseFactory;
    }

    protected override Task<IEnumerable<PrismCmsServiceBlueprintSchema>> GetChildItemsAsync(PrismCmsServiceBlueprintSchema? parent)
    {
        if (parent is not null) return Task.FromResult(Enumerable.Empty<PrismCmsServiceBlueprintSchema>());
        using var db = _databaseFactory.CreateDatabase();
        return Task.FromResult<IEnumerable<PrismCmsServiceBlueprintSchema>>(db.Fetch<PrismCmsServiceBlueprintSchema>());
    }

    protected override Task<IEnumerable<PrismCmsServiceBlueprintSchema>> GetFoldersAsync(PrismCmsServiceBlueprintSchema? parent) =>
        Task.FromResult(Enumerable.Empty<PrismCmsServiceBlueprintSchema>());

    protected override Task<PrismCmsServiceBlueprintSchema?> GetFromServiceAsync(PrismCmsServiceBlueprintSchema? item) =>
        Task.FromResult(default(PrismCmsServiceBlueprintSchema));

    protected override Task<IEnumerable<uSyncAction>> DeleteMissingItemsAsync(
        PrismCmsServiceBlueprintSchema parent, IEnumerable<Guid> keysToKeep, bool reportOnly) =>
        Task.FromResult(Enumerable.Empty<uSyncAction>());

    protected override string GetItemName(PrismCmsServiceBlueprintSchema item) => item.DisplayName;
}
