using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
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
/// Creates the <c>MobileNavItem</c> element type and associated data types on startup,
/// then replaces the multi-URL-picker <c>mobileNavLinks</c> property on Settings with a Block List.
/// Also adds a sibling <c>webNavLinks</c> Block List property, reusing the same element type, so
/// the desktop header nav can be authored independently of the mobile bottom bar — the two
/// surfaces genuinely carry different content today (see <c>DemoMobileNavSeeder</c>), and this
/// makes that difference backoffice-editable instead of one being hardcoded HTML.
/// Runs idempotently in Development only — skip if element type already exists.
/// </summary>
public class MobileNavSchemaSetup(
    IContentTypeService contentTypeService,
    IDataTypeService dataTypeService,
    IShortStringHelper shortStringHelper,
    PropertyEditorCollection propertyEditorCollection,
    IConfigurationEditorJsonSerializer configurationEditorJsonSerializer,
    IWebHostEnvironment env,
    IRuntimeState runtimeState,
    ILogger<MobileNavSchemaSetup> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    // Deterministic project-specific GUIDs — fixed so we can find/recreate reliably.
    private static readonly Guid MobileNavItemTypeKey  = new("a9f4b2c1-3d5e-6f70-8912-34abc5678def");
    private static readonly Guid MobileNavIconPickerKey = new("b8e3a1d0-2c4f-5e69-7801-23bcd4567efa");
    private static readonly Guid MobileNavBlockListKey  = new("c7d2f0e9-1b3a-4d58-6790-12cde3456fe9");
    private static readonly Guid WebNavBlockListKey     = new("d1e2f3a4-5b6c-4d7e-8f90-123456789abc");

    // Well-known Umbraco built-in data type GUIDs (stable across all Umbraco v14+ installs).
    private static readonly Guid BuiltInTextBoxKey  = new("0cc0eba1-9960-42c9-bf9b-60e150b429ae");
    private static readonly Guid BuiltInTrueFalseKey = new("92897bc6-a5f3-4ffe-ae27-f2e7e33dda49");

    public async Task HandleAsync(
        UmbracoApplicationStartedNotification notification,
        CancellationToken cancellationToken)
    {
        if (runtimeState.Level < RuntimeLevel.Run) return;
        if (!env.IsDevelopment()) return;

        logger.LogInformation("MOBILE NAV SCHEMA: Starting setup");
        await Task.Run(SetupSchemaAsync, cancellationToken);
        logger.LogInformation("MOBILE NAV SCHEMA: Setup complete");
    }

    private async Task SetupSchemaAsync()
    {
        try
        {
            // Idempotency guard — if element type already exists, patch property descriptions
            // and ensure the block list data type and Settings property are wired up.
            if (contentTypeService.Get("mobileNavItem") is { } existingElementType)
            {
                logger.LogDebug("MOBILE NAV SCHEMA: mobileNavItem element type already exists — patching descriptions.");
                PatchPropertyDescriptions(existingElementType);
                await GetOrCreateBlockListAsync(MobileNavItemTypeKey);
                await EnsureSettingsMobileNavPropertyAsync();
                await GetOrCreateWebNavBlockListAsync(MobileNavItemTypeKey);
                await EnsureSettingsWebNavPropertyAsync();
                return;
            }

            // Step B: Media Picker data type for icons.
            var iconPicker = await GetOrCreateIconPickerAsync();
            if (iconPicker == null)
            {
                logger.LogError("MOBILE NAV SCHEMA: Failed to create icon picker — aborting.");
                return;
            }

            // Step A: Built-in data types for label/url/toggle.
            var textBox = await dataTypeService.GetAsync(BuiltInTextBoxKey);
            var trueFalse = await dataTypeService.GetAsync(BuiltInTrueFalseKey);
            if (textBox == null || trueFalse == null)
            {
                logger.LogError("MOBILE NAV SCHEMA: Could not resolve built-in Textstring/TrueFalse data types.");
                return;
            }

            // Step A: Create the MobileNavItem element type.
            var elementType = new ContentType(shortStringHelper, -1)
            {
                Key = MobileNavItemTypeKey,
                Alias = "mobileNavItem",
                Name = "Mobile Nav Item",
                IsElement = true,
                Icon = "icon-navigation"
            };

            const string groupName = "Navigation";
            elementType.AddPropertyGroup(groupName, "navigation");

            elementType.AddPropertyType(new PropertyType(shortStringHelper, textBox, "navLabel")
            {
                Name = "Label",
                Description = "The text label shown below the icon in the mobile navigation bar",
                Mandatory = false,
                SortOrder = 0
            }, groupName);

            elementType.AddPropertyType(new PropertyType(shortStringHelper, textBox, "navUrl")
            {
                Name = "URL",
                Description = "The path this navigation item links to (e.g. / or /dashboard)",
                Mandatory = false,
                SortOrder = 1
            }, groupName);

            elementType.AddPropertyType(new PropertyType(shortStringHelper, iconPicker, "navIcon")
            {
                Name = "Icon",
                Description = "Pick a media item from the library to use as the navigation icon",
                Mandatory = false,
                SortOrder = 2
            }, groupName);

            elementType.AddPropertyType(new PropertyType(shortStringHelper, trueFalse, "openInNewTab")
            {
                Name = "Open in new tab",
                Mandatory = false,
                SortOrder = 3
            }, groupName);

#pragma warning disable CS0618 // No non-deprecated Save overload on IContentTypeService in v17.2.2
            contentTypeService.Save(elementType);
#pragma warning restore CS0618

            logger.LogInformation("MOBILE NAV SCHEMA: Created mobileNavItem element type ({Key}).", MobileNavItemTypeKey);

            // Step C: Block List data type that wraps MobileNavItem blocks.
            var blockList = await GetOrCreateBlockListAsync(elementType.Key);
            if (blockList == null)
            {
                logger.LogError("MOBILE NAV SCHEMA: Failed to create block list data type.");
                return;
            }

            // Step D: Wire up Settings.mobileNavLinks to use the new Block List.
            await EnsureSettingsMobileNavPropertyAsync(blockList);

            // Step E: same element type, a second dedicated Block List + Settings property for
            // the desktop nav — see class doc-comment for why these are separate properties.
            var webNavBlockList = await GetOrCreateWebNavBlockListAsync(elementType.Key);
            if (webNavBlockList == null)
            {
                logger.LogError("MOBILE NAV SCHEMA: Failed to create web nav block list data type.");
                return;
            }

            await EnsureSettingsWebNavPropertyAsync(webNavBlockList);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MOBILE NAV SCHEMA: Unexpected error during setup — safe to ignore.");
        }
    }

    private void PatchPropertyDescriptions(IContentType elementType)
    {
        bool dirty = false;

        var labelProp = elementType.PropertyTypes.FirstOrDefault(p => p.Alias == "navLabel");
        if (labelProp != null && string.IsNullOrWhiteSpace(labelProp.Description))
        {
            labelProp.Description = "The text label shown below the icon in the mobile navigation bar";
            dirty = true;
        }

        var urlProp = elementType.PropertyTypes.FirstOrDefault(p => p.Alias == "navUrl");
        if (urlProp != null && string.IsNullOrWhiteSpace(urlProp.Description))
        {
            urlProp.Description = "The path this navigation item links to (e.g. / or /dashboard)";
            dirty = true;
        }

        if (dirty)
        {
#pragma warning disable CS0618
            contentTypeService.Save(elementType);
#pragma warning restore CS0618
            logger.LogInformation("MOBILE NAV SCHEMA: Patched navLabel/navUrl property descriptions.");
        }
    }

    private async Task<IDataType?> GetOrCreateIconPickerAsync()
    {
        var existing = await dataTypeService.GetAsync(MobileNavIconPickerKey);
        if (existing != null)
        {
            logger.LogDebug("MOBILE NAV SCHEMA: Icon picker data type already exists.");
            return existing;
        }

        var editor = propertyEditorCollection["Umbraco.MediaPicker3"];
        if (editor == null)
        {
            logger.LogError("MOBILE NAV SCHEMA: Umbraco.MediaPicker3 editor not found in PropertyEditorCollection.");
            return null;
        }

        var dataType = new DataType(editor, configurationEditorJsonSerializer)
        {
            Key = MobileNavIconPickerKey,
            Name = "Mobile Nav Icon Picker",
            DatabaseType = ValueStorageType.Ntext,
            EditorUiAlias = "Umb.PropertyEditorUi.MediaPicker",
            ConfigurationData = new Dictionary<string, object>
            {
                { "multiple", false },
                // validationLimit: editors may pick 0 or 1 icon per nav item.
                { "validationLimit", new Dictionary<string, object> { { "min", 0 }, { "max", 1 } } }
            }
        };

        await dataTypeService.CreateAsync(dataType, Constants.Security.SuperUserKey);
        logger.LogInformation("MOBILE NAV SCHEMA: Created Mobile Nav Icon Picker data type.");
        return dataType;
    }

    private async Task<IDataType?> GetOrCreateBlockListAsync(Guid mobileNavItemKey)
    {
        var existing = await dataTypeService.GetAsync(MobileNavBlockListKey);
        if (existing != null)
        {
            logger.LogDebug("MOBILE NAV SCHEMA: Block List data type already exists.");
            return existing;
        }

        var editor = propertyEditorCollection["Umbraco.BlockList"];
        if (editor == null)
        {
            logger.LogError("MOBILE NAV SCHEMA: Umbraco.BlockList editor not found in PropertyEditorCollection.");
            return null;
        }

        var dataType = new DataType(editor, configurationEditorJsonSerializer)
        {
            Key = MobileNavBlockListKey,
            Name = "Mobile Nav Block List",
            DatabaseType = ValueStorageType.Ntext,
            EditorUiAlias = "Umb.PropertyEditorUi.BlockList",
            ConfigurationData = new Dictionary<string, object>
            {
                {
                    "blocks", new[]
                    {
                        new Dictionary<string, object>
                        {
                            { "contentElementTypeKey", mobileNavItemKey },
                            { "label", "{=navLabel}" }
                        }
                    }
                },
                { "validationLimit", new Dictionary<string, object> { { "min", 0 }, { "max", 4 } } }
            }
        };

        await dataTypeService.CreateAsync(dataType, Constants.Security.SuperUserKey);
        logger.LogInformation("MOBILE NAV SCHEMA: Created Mobile Nav Block List data type.");
        return dataType;
    }

    private async Task<IDataType?> GetOrCreateWebNavBlockListAsync(Guid mobileNavItemKey)
    {
        var existing = await dataTypeService.GetAsync(WebNavBlockListKey);
        if (existing != null)
        {
            logger.LogDebug("MOBILE NAV SCHEMA: Web nav block list data type already exists.");
            return existing;
        }

        var editor = propertyEditorCollection["Umbraco.BlockList"];
        if (editor == null)
        {
            logger.LogError("MOBILE NAV SCHEMA: Umbraco.BlockList editor not found in PropertyEditorCollection.");
            return null;
        }

        var dataType = new DataType(editor, configurationEditorJsonSerializer)
        {
            Key = WebNavBlockListKey,
            Name = "Web Nav Block List",
            DatabaseType = ValueStorageType.Ntext,
            EditorUiAlias = "Umb.PropertyEditorUi.BlockList",
            ConfigurationData = new Dictionary<string, object>
            {
                {
                    "blocks", new[]
                    {
                        new Dictionary<string, object>
                        {
                            { "contentElementTypeKey", mobileNavItemKey },
                            { "label", "{=navLabel}" }
                        }
                    }
                },
                { "validationLimit", new Dictionary<string, object> { { "min", 0 }, { "max", 6 } } }
            }
        };

        await dataTypeService.CreateAsync(dataType, Constants.Security.SuperUserKey);
        logger.LogInformation("MOBILE NAV SCHEMA: Created Web Nav Block List data type.");
        return dataType;
    }

    private async Task EnsureSettingsWebNavPropertyAsync(IDataType? blockListDataType = null)
    {
        var settings = contentTypeService.Get("settings");
        if (settings == null)
        {
            logger.LogDebug("MOBILE NAV SCHEMA: Settings content type not found — skipping webNavLinks update.");
            return;
        }

        blockListDataType ??= await dataTypeService.GetAsync(WebNavBlockListKey);
        if (blockListDataType == null)
        {
            logger.LogDebug("MOBILE NAV SCHEMA: Web nav block list data type not found — cannot update Settings property.");
            return;
        }

        const string propertyAlias = "webNavLinks";
        if (settings.PropertyTypes.Any(p => p.Alias == propertyAlias))
        {
            logger.LogDebug("MOBILE NAV SCHEMA: Settings.webNavLinks already exists — nothing to do.");
            return;
        }

        const string groupName = "Web Navigation";
        if (!settings.PropertyGroups.Any(g => g.Name == groupName))
            settings.AddPropertyGroup(groupName, "webNavigation");

        settings.AddPropertyType(new PropertyType(shortStringHelper, blockListDataType, propertyAlias)
        {
            Name = "Web Navigation Links",
            Description = "Navigation items for the desktop site header. Each item has a label, URL, icon (optional), and open-in-new-tab toggle.",
            Mandatory = false,
            SortOrder = 0
        }, groupName);

#pragma warning disable CS0618
        contentTypeService.Save(settings);
#pragma warning restore CS0618

        logger.LogInformation("MOBILE NAV SCHEMA: Settings.webNavLinks created using Web Nav Block List.");
    }

    private async Task EnsureSettingsMobileNavPropertyAsync(IDataType? blockListDataType = null)
    {
        var settings = contentTypeService.Get("settings");
        if (settings == null)
        {
            logger.LogDebug("MOBILE NAV SCHEMA: Settings content type not found — skipping property update.");
            return;
        }

        blockListDataType ??= await dataTypeService.GetAsync(MobileNavBlockListKey);
        if (blockListDataType == null)
        {
            logger.LogDebug("MOBILE NAV SCHEMA: Block list data type not found — cannot update Settings property.");
            return;
        }

        const string propertyAlias = "mobileNavLinks";
        var existing = settings.PropertyTypes.FirstOrDefault(p => p.Alias == propertyAlias);

        if (existing != null)
        {
            if (existing.DataTypeKey == blockListDataType.Key)
            {
                logger.LogDebug("MOBILE NAV SCHEMA: Settings.mobileNavLinks already uses Block List — nothing to do.");
                return;
            }

            // Property exists but uses old data type (Multi URL Picker) — replace it.
            logger.LogInformation("MOBILE NAV SCHEMA: Replacing Settings.mobileNavLinks ({OldKey}) → Block List ({NewKey}).",
                existing.DataTypeKey, blockListDataType.Key);

            settings.RemovePropertyType(propertyAlias);
#pragma warning disable CS0618
            contentTypeService.Save(settings);
#pragma warning restore CS0618
            settings = contentTypeService.Get("settings")!;
            if (settings == null) return;
        }

        const string groupName = "Mobile Navigation";
        if (!settings.PropertyGroups.Any(g => g.Name == groupName))
            settings.AddPropertyGroup(groupName, "mobileNavigation");

        settings.AddPropertyType(new PropertyType(shortStringHelper, blockListDataType, propertyAlias)
        {
            Name = "Mobile Navigation Links",
            Description = "Up to 4 navigation items for the mobile app bottom bar. Each item has a label, URL, icon (from media library), and open-in-new-tab toggle.",
            Mandatory = false,
            SortOrder = 0
        }, groupName);

#pragma warning disable CS0618
        contentTypeService.Save(settings);
#pragma warning restore CS0618

        logger.LogInformation("MOBILE NAV SCHEMA: Settings.mobileNavLinks now uses Mobile Nav Block List.");
    }
}
