using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;

namespace UmbracoPrism.Core;

/// <summary>
/// Ensures required Prism document types (and their templates) exist on startup.
/// Runs idempotently — only creates types/templates if they don't already exist.
/// </summary>
public class PrismContentTypeSeeder(
    IContentTypeService contentTypeService,
    ITemplateService templateService,
    IShortStringHelper shortStringHelper,
    IDataTypeService dataTypeService,
    IRuntimeState runtimeState)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    public async Task HandleAsync(
        UmbracoApplicationStartedNotification notification,
        CancellationToken cancellationToken)
    {
        if (runtimeState.Level < RuntimeLevel.Run) return;

        await EnsureDocumentTypeAsync("homePage", "Home Page", allowedAsRoot: true);
        await EnsureDocumentTypeAsync("memberDashboard", "Member Dashboard", allowedAsRoot: false);
        await EnsureSettingsDocumentTypeAsync();
    }

    private async Task EnsureDocumentTypeAsync(string alias, string name, bool allowedAsRoot)
    {
        var contentType = contentTypeService.Get(alias);

        if (contentType == null)
        {
            contentType = new ContentType(shortStringHelper, -1)
            {
                Alias = alias,
                Name = name,
                AllowedAsRoot = allowedAsRoot,
                Icon = alias == "homePage" ? "icon-home" : "icon-dashboard"
            };
#pragma warning disable CS0618 // No non-deprecated Create overload exists on IContentTypeService in v17
            contentTypeService.Save(contentType);
#pragma warning restore CS0618
        }

        await EnsureTemplateAsync(contentType, name);
    }

    private async Task EnsureSettingsDocumentTypeAsync()
    {
        const string alias = "settings";
        var contentType = contentTypeService.Get(alias);

        if (contentType != null)
        {
            // Already exists - check if it has the property
            await EnsureMobileNavPropertyAsync(contentType);
            return;
        }

        // Create the Settings document type
        contentType = new ContentType(shortStringHelper, -1)
        {
            Alias = alias,
            Name = "Settings",
            AllowedAsRoot = true,
            Icon = "icon-settings-alt",
            IsElement = false
        };

#pragma warning disable CS0618
        contentTypeService.Save(contentType);
#pragma warning restore CS0618

        // Add the mobile nav property
        await EnsureMobileNavPropertyAsync(contentType);
    }

    private async Task EnsureMobileNavPropertyAsync(IContentType contentType)
    {
        const string propertyAlias = "mobileNavLinks";
        
        // Skip if property already exists
        if (contentType.PropertyTypes.Any(p => p.Alias == propertyAlias))
        {
            return;
        }

        // Use the built-in Multi URL Picker data type (well-known Key in Umbraco)
        var multiUrlPickerKey = new Guid("fd1e0da5-5606-4862-b679-5d0cf3a52a59");
        var dataType = await dataTypeService.GetAsync(multiUrlPickerKey);
        
        if (dataType == null)
        {
            // Fallback: if the built-in doesn't exist, skip this property
            return;
        }

        // Add property group if it doesn't exist
        const string groupName = "Mobile Navigation";
        const string groupKey = "mobileNavigation";
        if (!contentType.PropertyGroups.Any(g => g.Name == groupName))
        {
            contentType.AddPropertyGroup(groupName, groupKey);
        }

        // Add the property
        var propertyType = new PropertyType(shortStringHelper, dataType, propertyAlias)
        {
            Name = "Mobile Navigation Links",
            Description = "Configure up to 4 navigation links for the mobile app bottom navigation bar (max 4 items recommended)",
            Mandatory = false,
            SortOrder = 0
        };

        contentType.AddPropertyType(propertyType, groupName);
        
#pragma warning disable CS0618
        contentTypeService.Save(contentType);
#pragma warning restore CS0618
    }

    private async Task EnsureTemplateAsync(IContentType contentType, string templateName)
    {
        // Skip if the doc type already has an allowed template assigned
        if (contentType.AllowedTemplates?.Any() == true) return;

        // Create the template if it doesn't exist yet.
        // CreateForContentTypeAsync creates the template file and DB record but does NOT
        // update the doc type — we must assign it separately.
        var template = await templateService.GetAsync(contentType.Alias);
        if (template == null)
        {
            var attempt = await templateService.CreateForContentTypeAsync(
                contentType.Alias, templateName, Constants.Security.SuperUserKey);
            template = attempt.Result;
        }

        if (template == null) return;

        contentType.AllowedTemplates = [template];
        contentType.SetDefaultTemplate(template);
#pragma warning disable CS0618 // IContentTypeService has no non-deprecated Save replacement in v17.2.2
        contentTypeService.Save(contentType);
#pragma warning restore CS0618
    }
}
