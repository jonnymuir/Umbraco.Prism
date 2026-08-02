using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;

namespace UmbracoPrism.TestSite;

/// <summary>
/// Ensures the <c>publicServiceRequestPage</c> document type exists — TestSite's own anonymous-first,
/// in-Umbraco-hosted service blueprint page, route-hijacked by
/// <see cref="Controllers.PublicServiceRequestPageController"/>. Distinct from <c>stagePage</c>
/// (the member-authenticated business-service-blueprint demo pattern, talking to a remote
/// Business App) — this one runs entirely in-process against Wayfinder.Umbraco's own engine, no
/// host-side content-type opinion from Core. Runs unconditionally (like Core's own
/// <c>PrismContentTypeSeeder</c>, not just in Development) since content-type schema is safe to
/// ensure in every environment; only the demo content built on top of it is dev-gated
/// (<see cref="JugglingLicenceServiceRequestSeeder"/>, <see cref="LicenceTransferServiceRequestSeeder"/>).
/// </summary>
/// <remarks>
/// <see cref="ComposeAfterAttribute"/> on <see cref="PrismComposer"/> guarantees
/// <c>homePage</c> already exists by the time this runs.
/// </remarks>
public class PublicServiceRequestContentType(
    IContentTypeService contentTypeService,
    IDataTypeService dataTypeService,
    IConfigurationEditorJsonSerializer configurationEditorJsonSerializer,
    PropertyEditorCollection propertyEditorCollection,
    ITemplateService templateService,
    IShortStringHelper shortStringHelper,
    IRuntimeState runtimeState,
    ILogger<PublicServiceRequestContentType> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    public const string Alias = "publicServiceRequestPage";

    private static readonly Guid TextboxDataTypeKey = new("d1e2f3a4-b5c6-4d7e-8f9a-0b1c2d3e4f5a");

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (runtimeState.Level < RuntimeLevel.Run) return;

        var contentType = contentTypeService.Get(Alias);

        if (contentType == null)
        {
            contentType = new ContentType(shortStringHelper, -1)
            {
                Alias = Alias,
                Name = "Service Request Page",
                AllowedAsRoot = true,
                Icon = "icon-diagram"
            };
#pragma warning disable CS0618
            contentTypeService.Save(contentType);
#pragma warning restore CS0618
        }
        else if (contentType.AllowedAsRoot)
        {
            contentType.AllowedAsRoot = false;
#pragma warning disable CS0618
            contentTypeService.Save(contentType);
#pragma warning restore CS0618
        }

        await EnsureBlueprintKeyPropertyAsync(contentType);
        await EnsureTemplateAsync(contentType);
        await EnsureHomeAllowsChildAsync();
    }

    private async Task EnsureBlueprintKeyPropertyAsync(IContentType contentType)
    {
        const string propertyAlias = "blueprintKey";
        if (contentType.PropertyTypes.Any(p => p.Alias == propertyAlias)) return;

        var textboxDataType = await GetOrCreateTextboxDataTypeAsync();
        if (textboxDataType == null)
        {
            logger.LogWarning("TESTSITE: Could not resolve textbox data type; skipping blueprintKey property on publicServiceRequestPage");
            return;
        }

        const string groupName = "Service Blueprint Configuration";
        const string groupKey = "serviceBlueprintConfiguration";
        if (!contentType.PropertyGroups.Any(g => g.Name == groupName))
            contentType.AddPropertyGroup(groupName, groupKey);

        var propertyType = new PropertyType(shortStringHelper, textboxDataType, propertyAlias)
        {
            Name = "Blueprint Key",
            Description = "The service blueprint key to run on this page (e.g. 'apply-for-a-juggling-licence').",
            Mandatory = false,
            SortOrder = 0
        };

        contentType.AddPropertyType(propertyType, groupName);

#pragma warning disable CS0618
        contentTypeService.Save(contentType);
#pragma warning restore CS0618
        logger.LogInformation("TESTSITE: blueprintKey property added to publicServiceRequestPage content type");
    }

    private async Task<IDataType?> GetOrCreateTextboxDataTypeAsync()
    {
        const string editorAlias = "Umbraco.TextBox";

        var existing = await dataTypeService.GetAsync(TextboxDataTypeKey);
        if (existing != null) return existing;

        var editor = propertyEditorCollection[editorAlias];
        if (editor == null)
        {
            logger.LogError("TESTSITE: Editor '{EditorAlias}' not found", editorAlias);
            return null;
        }

        var newDataType = new DataType(editor, configurationEditorJsonSerializer)
        {
            Key = TextboxDataTypeKey,
            Name = "TestSite Textbox",
            DatabaseType = ValueStorageType.Nvarchar,
            EditorUiAlias = "Umb.PropertyEditorUi.TextBox"
        };

        await dataTypeService.CreateAsync(newDataType, Constants.Security.SuperUserKey);
        return newDataType;
    }

    private async Task EnsureTemplateAsync(IContentType contentType)
    {
        if (contentType.AllowedTemplates?.Any() == true) return;

        var template = await templateService.GetAsync(contentType.Alias);
        if (template == null)
        {
            var attempt = await templateService.CreateForContentTypeAsync(
                "Service Request Page", contentType.Alias, contentType.Alias, Constants.Security.SuperUserKey);
            template = attempt.Result;
        }

        if (template == null) return;

        contentType.AllowedTemplates = [template];
        contentType.SetDefaultTemplate(template);
#pragma warning disable CS0618
        contentTypeService.Save(contentType);
#pragma warning restore CS0618
    }

    private async Task EnsureHomeAllowsChildAsync()
    {
        var homePage = contentTypeService.Get("homePage");
        var serviceRequestPage = contentTypeService.Get(Alias);
        if (homePage == null || serviceRequestPage == null) return;

        var existingAliases = (homePage.AllowedContentTypes ?? []).Select(sort => sort.Alias).ToHashSet();
        if (existingAliases.Contains(Alias)) return;

        homePage.AllowedContentTypes = (homePage.AllowedContentTypes ?? [])
            .Append(new ContentTypeSort(serviceRequestPage.Key, existingAliases.Count, serviceRequestPage.Alias))
            .DistinctBy(sort => sort.Alias);

#pragma warning disable CS0618
        contentTypeService.Save(homePage);
#pragma warning restore CS0618

        logger.LogInformation("TESTSITE: homePage now allows publicServiceRequestPage as a child");
        await Task.CompletedTask;
    }
}
