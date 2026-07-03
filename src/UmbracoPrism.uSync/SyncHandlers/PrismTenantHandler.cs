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

[SyncHandler("PrismTenantHandler", "Prism Tenants", "Tenants",
    BackOfficeConsts.Priorites.USYNC_RESERVED_UPPER + 100,
    Icon = "icon-user",
    EntityType = "prismTenant")]
public class PrismTenantHandler : SyncHandlerRoot<PrismTenantSchema, PrismTenantSchema>, ISyncHandler
{
    private readonly IUmbracoDatabaseFactory _databaseFactory;

    public override string Group => BackOfficeConsts.Groups.Settings;

    public PrismTenantHandler(
        ILogger<SyncHandlerRoot<PrismTenantSchema, PrismTenantSchema>> logger,
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

    protected override Task<IEnumerable<PrismTenantSchema>> GetChildItemsAsync(PrismTenantSchema? parent)
    {
        if (parent is not null) return Task.FromResult(Enumerable.Empty<PrismTenantSchema>());
        using var db = _databaseFactory.CreateDatabase();
        return Task.FromResult<IEnumerable<PrismTenantSchema>>(db.Fetch<PrismTenantSchema>());
    }

    protected override Task<IEnumerable<PrismTenantSchema>> GetFoldersAsync(PrismTenantSchema? parent) =>
        Task.FromResult(Enumerable.Empty<PrismTenantSchema>());

    protected override Task<PrismTenantSchema?> GetFromServiceAsync(PrismTenantSchema? item) =>
        Task.FromResult(default(PrismTenantSchema));

    protected override Task<IEnumerable<uSyncAction>> DeleteMissingItemsAsync(
        PrismTenantSchema parent, IEnumerable<Guid> keysToKeep, bool reportOnly) =>
        Task.FromResult(Enumerable.Empty<uSyncAction>());

    protected override string GetItemName(PrismTenantSchema item) => item.Name;
}
