using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
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
    IConfigurationEditorJsonSerializer configurationEditorJsonSerializer,
    PropertyEditorCollection propertyEditorCollection,
    IRuntimeState runtimeState,
    ILogger<PrismContentTypeSeeder> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    // Deterministic project-specific GUID for the Prism Mobile Nav data type.
    // Using a fixed key means we can always find/create it reliably without name-based lookups.
    private static readonly Guid PrismMobileNavDataTypeKey = new Guid("3b4c5d6e-7f80-9a1b-c2d3-e4f567890abc");

    public async Task HandleAsync(
        UmbracoApplicationStartedNotification notification,
        CancellationToken cancellationToken)
    {
        if (runtimeState.Level < RuntimeLevel.Run) return;

        logger.LogInformation("PRISM ContentTypeSeeder: Starting");

        await EnsureDocumentTypeAsync("homePage", "Home Page", allowedAsRoot: true);
        await EnsureDocumentTypeAsync("memberDashboard", "Member Dashboard", allowedAsRoot: false);
        await EnsureWorkflowDemoPageAsync();
        await EnsureSettingsDocumentTypeAsync();

        logger.LogInformation("PRISM ContentTypeSeeder: Complete");
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

        if (alias == "homePage")
        {
            await EnsureHeroImagePropertyAsync(contentType);
        }

        await EnsureTemplateAsync(contentType, name);
    }

    private async Task EnsureWorkflowDemoPageAsync()
    {
        const string alias = "workflowDemoPage";
        const string name = "Workflow Demo Page";
        
        var contentType = contentTypeService.Get(alias);

        if (contentType == null)
        {
            contentType = new ContentType(shortStringHelper, -1)
            {
                Alias = alias,
                Name = name,
                AllowedAsRoot = true,
                Icon = "icon-activity"
            };
#pragma warning disable CS0618
            contentTypeService.Save(contentType);
#pragma warning restore CS0618
        }

        // Add properties for workflow demo page
        await EnsureWorkflowDemoPropertiesAsync(contentType);
        await EnsureTemplateAsync(contentType, name);
    }

    private async Task EnsureWorkflowDemoPropertiesAsync(IContentType contentType)
    {
        const string groupName = "Workflow Configuration";
        const string groupKey = "workflowConfiguration";
        
        if (!contentType.PropertyGroups.Any(g => g.Name == groupName))
        {
            contentType.AddPropertyGroup(groupName, groupKey);
        }

        // Create data types for workflow demo properties
        var textboxDataType = await GetOrCreateTextboxDataTypeAsync();
        var textareaDataType = await GetOrCreateTextareaDataTypeAsync();

        if (textboxDataType == null || textareaDataType == null)
        {
            logger.LogWarning("PRISM: Could not create textbox or textarea data types");
            return;
        }

        bool modified = false;

        // Add workflowDefinitionKey property
        if (!contentType.PropertyTypes.Any(p => p.Alias == "workflowDefinitionKey"))
        {
            var propertyType = new PropertyType(shortStringHelper, textboxDataType, "workflowDefinitionKey")
            {
                Name = "Workflow Definition Key",
                Description = "The key of the workflow to render (e.g., 'information-request')",
                Mandatory = false,
                SortOrder = 0
            };
            contentType.AddPropertyType(propertyType, groupName);
            modified = true;
        }

        // Add pageTitle property
        if (!contentType.PropertyTypes.Any(p => p.Alias == "pageTitle"))
        {
            var propertyType = new PropertyType(shortStringHelper, textboxDataType, "pageTitle")
            {
                Name = "Page Title",
                Description = "H1 heading displayed on the page",
                Mandatory = false,
                SortOrder = 1
            };
            contentType.AddPropertyType(propertyType, groupName);
            modified = true;
        }

        // Add pageIntro property
        if (!contentType.PropertyTypes.Any(p => p.Alias == "pageIntro"))
        {
            var propertyType = new PropertyType(shortStringHelper, textareaDataType, "pageIntro")
            {
                Name = "Page Introduction",
                Description = "Introductory text displayed above the workflow form",
                Mandatory = false,
                SortOrder = 2
            };
            contentType.AddPropertyType(propertyType, groupName);
            modified = true;
        }

        if (modified)
        {
#pragma warning disable CS0618
            contentTypeService.Save(contentType);
#pragma warning restore CS0618
            logger.LogInformation("PRISM: Workflow demo page properties added");
        }
    }

    private static readonly Guid PrismTextboxDataTypeKey = new Guid("5e6f7a8b-9c0d-1e2f-3a4b-5c6d7e8f9a0b");
    private static readonly Guid PrismTextareaDataTypeKey = new Guid("6f7a8b9c-0d1e-2f3a-4b5c-6d7e8f9a0b1c");

    private async Task<IDataType?> GetOrCreateTextboxDataTypeAsync()
    {
        const string editorAlias = "Umbraco.TextBox";
        const string dataTypeName = "Prism Textbox";

        var existing = await dataTypeService.GetAsync(PrismTextboxDataTypeKey);
        if (existing != null) return existing;

        var editor = propertyEditorCollection[editorAlias];
        if (editor == null)
        {
            logger.LogError("PRISM: Editor '{EditorAlias}' not found", editorAlias);
            return null;
        }

        var newDataType = new DataType(editor, configurationEditorJsonSerializer)
        {
            Key = PrismTextboxDataTypeKey,
            Name = dataTypeName,
            DatabaseType = ValueStorageType.Nvarchar,
            EditorUiAlias = "Umb.PropertyEditorUi.TextBox"
        };

        await dataTypeService.CreateAsync(newDataType, Constants.Security.SuperUserKey);
        return newDataType;
    }

    private async Task<IDataType?> GetOrCreateTextareaDataTypeAsync()
    {
        const string editorAlias = "Umbraco.TextArea";
        const string dataTypeName = "Prism Textarea";

        var existing = await dataTypeService.GetAsync(PrismTextareaDataTypeKey);
        if (existing != null) return existing;

        var editor = propertyEditorCollection[editorAlias];
        if (editor == null)
        {
            logger.LogError("PRISM: Editor '{EditorAlias}' not found", editorAlias);
            return null;
        }

        var newDataType = new DataType(editor, configurationEditorJsonSerializer)
        {
            Key = PrismTextareaDataTypeKey,
            Name = dataTypeName,
            DatabaseType = ValueStorageType.Ntext,
            EditorUiAlias = "Umb.PropertyEditorUi.TextArea"
        };

        await dataTypeService.CreateAsync(newDataType, Constants.Security.SuperUserKey);
        return newDataType;
    }

    private static readonly Guid PrismMediaPickerDataTypeKey = new Guid("a2b3c4d5-e6f7-8901-a2b3-c4d5e6f78901");

    private async Task EnsureHeroImagePropertyAsync(IContentType contentType)
    {
        const string propertyAlias = "heroImage";
        if (contentType.PropertyTypes.Any(p => p.Alias == propertyAlias)) return;

        const string editorAlias = "Umbraco.MediaPicker3";
        const string dataTypeName = "Prism Hero Image Picker";

        var dataType = await dataTypeService.GetAsync(PrismMediaPickerDataTypeKey);
        if (dataType == null)
        {
            var editor = propertyEditorCollection[editorAlias];
            if (editor == null)
            {
                logger.LogWarning("PRISM: Editor '{EditorAlias}' not found; skipping heroImage property", editorAlias);
                return;
            }

            dataType = new DataType(editor, configurationEditorJsonSerializer)
            {
                Key = PrismMediaPickerDataTypeKey,
                Name = dataTypeName,
                DatabaseType = ValueStorageType.Ntext,
                EditorUiAlias = "Umb.PropertyEditorUi.MediaPicker",
                ConfigurationData = new Dictionary<string, object>
                {
                    { "multiple", false },
                    { "onlyImages", true }
                }
            };

            await dataTypeService.CreateAsync(dataType, Constants.Security.SuperUserKey);
        }

        const string groupName = "Content";
        const string groupKey = "content";
        if (!contentType.PropertyGroups.Any(g => g.Name == groupName))
        {
            contentType.AddPropertyGroup(groupName, groupKey);
        }

        var propertyType = new PropertyType(shortStringHelper, dataType, propertyAlias)
        {
            Name = "Hero Image",
            Description = "Background image for the hero section. Pick from the media library.",
            Mandatory = false,
            SortOrder = 0
        };

        contentType.AddPropertyType(propertyType, groupName);

#pragma warning disable CS0618
        contentTypeService.Save(contentType);
#pragma warning restore CS0618

        logger.LogInformation("PRISM: heroImage property added to homePage content type");
    }

    private async Task EnsureSettingsDocumentTypeAsync()
    {
        const string alias = "settings";
        var contentType = contentTypeService.Get(alias);

        if (contentType != null)
        {
            // Already exists — only add the property if it's completely missing.
            // Never replace an existing property; other components (e.g. the TestSite) may
            // intentionally use a different data type (Block List, etc.) and we must not fight them.
            var hasProperty = contentType.PropertyTypes.Any(p => p.Alias == "mobileNavLinks");
            if (!hasProperty)
                contentType = await EnsureMobileNavPropertyAsync(contentType) ?? contentType;
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
        contentType = await EnsureMobileNavPropertyAsync(contentType) ?? contentType;
    }

    private async Task<IContentType?> EnsureMobileNavPropertyAsync(IContentType contentType)
    {
        const string propertyAlias = "mobileNavLinks";

        var newDataType = await GetOrCreatePrismMobileNavDataTypeAsync();
        if (newDataType == null) return contentType;

        var existingProperty = contentType.PropertyTypes.FirstOrDefault(p => p.Alias == propertyAlias);

        if (existingProperty != null)
        {
            if (existingProperty.DataTypeKey == newDataType.Key)
            {
                logger.LogDebug("PRISM: mobileNavLinks already uses correct data type {Key}", newDataType.Key);
                return contentType;
            }

            logger.LogInformation("PRISM: mobileNavLinks has wrong data type {OldKey}, removing and re-adding with {NewKey}",
                existingProperty.DataTypeKey, newDataType.Key);

            // Remove the property type entirely and fall through to re-create it correctly
            contentType.RemovePropertyType(propertyAlias);
#pragma warning disable CS0618
            contentTypeService.Save(contentType);
#pragma warning restore CS0618

            // Re-fetch the content type to get fresh state from DB (avoid stale cache)
            contentType = contentTypeService.Get(contentType.Alias)!;
            if (contentType == null) return null;
        }

        // Property doesn't exist yet — create it
        const string groupName = "Mobile Navigation";
        const string groupKey = "mobileNavigation";
        if (!contentType.PropertyGroups.Any(g => g.Name == groupName))
        {
            contentType.AddPropertyGroup(groupName, groupKey);
        }

        var propertyType = new PropertyType(shortStringHelper, newDataType, propertyAlias)
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

        var savedProperty = contentType.PropertyTypes.FirstOrDefault(p => p.Alias == propertyAlias);
        logger.LogInformation("PRISM: mobileNavLinks property data type key: {Key}", savedProperty?.DataTypeKey);

        return contentType;
    }

    private async Task<IDataType?> GetOrCreatePrismMobileNavDataTypeAsync()
    {
        const string editorAlias = "Umbraco.MultiUrlPicker";
        const string dataTypeName = "Prism Mobile Nav Links";

        logger.LogInformation("PRISM: Getting or creating data type. Fixed key: {Key}", PrismMobileNavDataTypeKey);

        // Try to find by our deterministic fixed GUID
        var existing = await dataTypeService.GetAsync(PrismMobileNavDataTypeKey);

        if (existing != null)
        {
            if (existing.EditorAlias == editorAlias)
            {
                // Fix EditorUiAlias if it's wrong (covers existing installations created before this fix)
                if (existing.EditorUiAlias != "Umb.PropertyEditorUi.MultiUrlPicker")
                {
                    logger.LogInformation("PRISM: Fixing EditorUiAlias on existing data type (was '{Old}')", existing.EditorUiAlias);
                    existing.EditorUiAlias = "Umb.PropertyEditorUi.MultiUrlPicker";
                    await dataTypeService.UpdateAsync(existing, Constants.Security.SuperUserKey);
                }
                logger.LogInformation("PRISM: Data type found/created with editor {EditorAlias}", existing.EditorAlias);
                return existing;
            }

            // Found but wrong editor — delete it (safe here as it's our own GUID, pre-content-type creation path)
            logger.LogWarning("PRISM: Data type at fixed key has wrong editor {WrongAlias}. Deleting and recreating.", existing.EditorAlias);
            var deleteAttempt = await dataTypeService.DeleteAsync(PrismMobileNavDataTypeKey, Constants.Security.SuperUserKey);
            if (!deleteAttempt.Success)
            {
                logger.LogError("PRISM: Failed to delete wrong data type: {Error}", deleteAttempt.Exception?.Message);
            }
        }

        // Create fresh with our fixed GUID
        var editor = propertyEditorCollection[editorAlias];
        if (editor == null)
        {
            logger.LogError("PRISM: Editor '{EditorAlias}' not found in PropertyEditorCollection", editorAlias);
            return null;
        }

        var newDataType = new DataType(editor, configurationEditorJsonSerializer)
        {
            Key = PrismMobileNavDataTypeKey,
            Name = dataTypeName,
            DatabaseType = ValueStorageType.Ntext,
            EditorUiAlias = "Umb.PropertyEditorUi.MultiUrlPicker",
            ConfigurationData = new Dictionary<string, object> { { "maxNumber", 4 } }
        };

        await dataTypeService.CreateAsync(newDataType, Constants.Security.SuperUserKey);
        logger.LogInformation("PRISM: Data type found/created with editor {EditorAlias}", newDataType.EditorAlias);
        return newDataType;
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
