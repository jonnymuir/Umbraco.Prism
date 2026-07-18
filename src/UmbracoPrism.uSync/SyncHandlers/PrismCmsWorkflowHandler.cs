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
/// uSync handler for backoffice-authored CMS Workflow definitions — mirrors
/// <see cref="PrismTenantHandler"/> exactly, giving CMS Workflow definitions the same
/// export/import portability Tenants already have.
/// </summary>
[SyncHandler("PrismCmsWorkflowHandler", "Prism CMS Workflows", "CmsWorkflows",
    BackOfficeConsts.Priorites.USYNC_RESERVED_UPPER + 101,
    Icon = "icon-diagram",
    EntityType = "prismCmsWorkflow")]
public class PrismCmsWorkflowHandler : SyncHandlerRoot<PrismCmsWorkflowDefinitionSchema, PrismCmsWorkflowDefinitionSchema>, ISyncHandler
{
    private readonly IUmbracoDatabaseFactory _databaseFactory;

    public override string Group => BackOfficeConsts.Groups.Settings;

    public PrismCmsWorkflowHandler(
        ILogger<SyncHandlerRoot<PrismCmsWorkflowDefinitionSchema, PrismCmsWorkflowDefinitionSchema>> logger,
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

    protected override Task<IEnumerable<PrismCmsWorkflowDefinitionSchema>> GetChildItemsAsync(PrismCmsWorkflowDefinitionSchema? parent)
    {
        if (parent is not null) return Task.FromResult(Enumerable.Empty<PrismCmsWorkflowDefinitionSchema>());
        using var db = _databaseFactory.CreateDatabase();
        return Task.FromResult<IEnumerable<PrismCmsWorkflowDefinitionSchema>>(db.Fetch<PrismCmsWorkflowDefinitionSchema>());
    }

    protected override Task<IEnumerable<PrismCmsWorkflowDefinitionSchema>> GetFoldersAsync(PrismCmsWorkflowDefinitionSchema? parent) =>
        Task.FromResult(Enumerable.Empty<PrismCmsWorkflowDefinitionSchema>());

    protected override Task<PrismCmsWorkflowDefinitionSchema?> GetFromServiceAsync(PrismCmsWorkflowDefinitionSchema? item) =>
        Task.FromResult(default(PrismCmsWorkflowDefinitionSchema));

    protected override Task<IEnumerable<uSyncAction>> DeleteMissingItemsAsync(
        PrismCmsWorkflowDefinitionSchema parent, IEnumerable<Guid> keysToKeep, bool reportOnly) =>
        Task.FromResult(Enumerable.Empty<uSyncAction>());

    protected override string GetItemName(PrismCmsWorkflowDefinitionSchema item) => item.DisplayName;
}
