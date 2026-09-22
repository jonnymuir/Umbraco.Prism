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

[SyncHandler("PrismPageAccessPolicyHandler", "Prism Page Access", "PageAccess",
    BackOfficeConsts.Priorites.USYNC_RESERVED_UPPER + 101,
    Icon = "icon-lock",
    EntityType = "prismPageAccessPolicy")]
public class PrismPageAccessPolicyHandler : SyncHandlerRoot<PrismPageAccessPolicySchema, PrismPageAccessPolicySchema>, ISyncHandler
{
    private readonly IUmbracoDatabaseFactory _databaseFactory;

    public override string Group => BackOfficeConsts.Groups.Settings;

    public PrismPageAccessPolicyHandler(
        ILogger<SyncHandlerRoot<PrismPageAccessPolicySchema, PrismPageAccessPolicySchema>> logger,
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

    protected override Task<IEnumerable<PrismPageAccessPolicySchema>> GetChildItemsAsync(PrismPageAccessPolicySchema? parent)
    {
        if (parent is not null) return Task.FromResult(Enumerable.Empty<PrismPageAccessPolicySchema>());
        using var db = _databaseFactory.CreateDatabase();
        return Task.FromResult<IEnumerable<PrismPageAccessPolicySchema>>(db.Fetch<PrismPageAccessPolicySchema>());
    }

    protected override Task<IEnumerable<PrismPageAccessPolicySchema>> GetFoldersAsync(PrismPageAccessPolicySchema? parent) =>
        Task.FromResult(Enumerable.Empty<PrismPageAccessPolicySchema>());

    protected override Task<PrismPageAccessPolicySchema?> GetFromServiceAsync(PrismPageAccessPolicySchema? item) =>
        Task.FromResult(default(PrismPageAccessPolicySchema));

    protected override Task<IEnumerable<uSyncAction>> DeleteMissingItemsAsync(
        PrismPageAccessPolicySchema parent, IEnumerable<Guid> keysToKeep, bool reportOnly) =>
        Task.FromResult(Enumerable.Empty<uSyncAction>());

    protected override string GetItemName(PrismPageAccessPolicySchema item) =>
        item.ContentRoute ?? $"policy-{item.Id}";
}
