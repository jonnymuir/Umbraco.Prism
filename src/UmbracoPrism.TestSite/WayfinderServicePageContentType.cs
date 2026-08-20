using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;

namespace UmbracoPrism.TestSite;

/// <summary>
/// Ensures the <c>wayfinderServicePage</c> document type exists — TestSite's own Block
/// Grid-composable page, carrying two optional areas that wrap Wayfinder.Umbraco's own packaged
/// Block Grid data types (see Wayfinder.Umbraco/Persistence/CreateServiceRequestStageBlock.cs/
/// CreateServiceRequestWorklistBlock.cs). This is deliberately the only Wayfinder-shaped content
/// type TestSite owns — service design itself lives entirely in Wayfinder.Umbraco now; this page
/// just proves an ordinary CMS editor can compose it in.
/// </summary>
/// <remarks>
/// <see cref="ComposeAfterAttribute"/> on <see cref="PrismComposer"/> guarantees <c>homePage</c>
/// already exists, and Wayfinder.Umbraco's own migration (which ships before any
/// <see cref="UmbracoApplicationStartedNotification"/> handler runs) guarantees the two Block Grid
/// data types below already exist.
/// </remarks>
public class WayfinderServicePageContentType(
    IContentTypeService contentTypeService,
    IDataTypeService dataTypeService,
    ITemplateService templateService,
    IShortStringHelper shortStringHelper,
    IRuntimeState runtimeState,
    ILogger<WayfinderServicePageContentType> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    public const string Alias = "wayfinderServicePage";

    // The two Block Grid data types Wayfinder.Umbraco's own migration plan ships on install —
    // see CreateServiceRequestStageBlock.cs/CreateServiceRequestWorklistBlock.cs in that package.
    private static readonly Guid StageBlockGridDataTypeKey = new("7a3b2d4e-9c5f-4b2a-8d7e-3f8c6b0a2d41");
    private static readonly Guid WorklistBlockGridDataTypeKey = new("9c5d4f60-1e7b-4d4c-af90-5b0e8d2c4f63");

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (runtimeState.Level < RuntimeLevel.Run) return;

        var contentType = contentTypeService.Get(Alias);
        if (contentType == null)
        {
            contentType = new ContentType(shortStringHelper, -1)
            {
                Alias = Alias,
                Name = "Wayfinder Service Page",
                AllowedAsRoot = false,
                Icon = "icon-molecule-alt"
            };
#pragma warning disable CS0618
            contentTypeService.Save(contentType);
#pragma warning restore CS0618
        }

        await EnsureAreaPropertiesAsync(contentType);
        await EnsureTemplateAsync(contentType);
        EnsureHomeAllowsChild(contentType);
    }

    private async Task EnsureAreaPropertiesAsync(IContentType contentType)
    {
        var changed = false;

        if (!contentType.PropertyTypes.Any(p => p.Alias == "stageArea"))
        {
            var stageBlockGrid = await GetDataTypeOrThrowAsync(StageBlockGridDataTypeKey, "wayfinderServiceRequestStage");
            contentType.AddPropertyGroup("Content", "content");
            contentType.AddPropertyType(new PropertyType(shortStringHelper, stageBlockGrid, "stageArea")
            {
                Name = "Stage area",
                Description = "Drop a Wayfinder Service Request Stage block here.",
                Mandatory = false,
                SortOrder = 0
            }, "Content");
            changed = true;
        }

        if (!contentType.PropertyTypes.Any(p => p.Alias == "worklistArea"))
        {
            var worklistBlockGrid = await GetDataTypeOrThrowAsync(WorklistBlockGridDataTypeKey, "wayfinderServiceRequestWorklist");
            contentType.AddPropertyGroup("Content", "content");
            contentType.AddPropertyType(new PropertyType(shortStringHelper, worklistBlockGrid, "worklistArea")
            {
                Name = "Worklist area",
                Description = "Drop a Wayfinder Service Request Worklist block here.",
                Mandatory = false,
                SortOrder = 1
            }, "Content");
            changed = true;
        }

        if (changed)
        {
#pragma warning disable CS0618
            contentTypeService.Save(contentType);
#pragma warning restore CS0618
            logger.LogInformation("TESTSITE: wayfinderServicePage area properties ensured");
        }
    }

    private async Task<IDataType> GetDataTypeOrThrowAsync(Guid key, string label)
    {
        var dataType = await dataTypeService.GetAsync(key);
        return dataType ?? throw new InvalidOperationException(
            $"Wayfinder.Umbraco's {label} Block Grid data type ({key}) was not found — its migration " +
            "step should have run before this content type is ensured.");
    }

    private async Task EnsureTemplateAsync(IContentType contentType)
    {
        if (contentType.AllowedTemplates?.Any() == true) return;

        var template = await templateService.GetAsync(contentType.Alias);
        if (template == null)
        {
            var attempt = await templateService.CreateForContentTypeAsync(
                contentType.Name!, contentType.Alias, contentType.Alias, Constants.Security.SuperUserKey);
            template = attempt.Result;
        }

        if (template == null)
        {
            logger.LogWarning("TESTSITE: Could not create a template for wayfinderServicePage");
            return;
        }

        contentType.AllowedTemplates = [template];
        contentType.SetDefaultTemplate(template);
#pragma warning disable CS0618
        contentTypeService.Save(contentType);
#pragma warning restore CS0618
    }

    private void EnsureHomeAllowsChild(IContentType wayfinderServicePage)
    {
        var homePage = contentTypeService.Get("homePage");
        if (homePage == null) return;

        var existingAliases = (homePage.AllowedContentTypes ?? []).Select(sort => sort.Alias).ToHashSet();
        if (existingAliases.Contains(Alias)) return;

        homePage.AllowedContentTypes = (homePage.AllowedContentTypes ?? [])
            .Append(new ContentTypeSort(wayfinderServicePage.Key, existingAliases.Count, wayfinderServicePage.Alias))
            .DistinctBy(sort => sort.Alias);

#pragma warning disable CS0618
        contentTypeService.Save(homePage);
#pragma warning restore CS0618

        logger.LogInformation("TESTSITE: homePage now allows wayfinderServicePage as a child");
    }
}
