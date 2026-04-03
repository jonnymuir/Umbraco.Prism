using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;

namespace UmbracoPrism.TestSite;

/// <summary>
/// Creates Vinyl Vault document types on application startup.
/// Runs once to ensure content types are registered before seeder runs.
/// Development-only for demo purposes.
/// </summary>
public class VinylVaultContentTypes : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private readonly IContentTypeService _contentTypeService;
    private readonly IDataTypeService _dataTypeService;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly IWebHostEnvironment _env;
    private readonly IRuntimeState _runtimeState;
    private readonly ILogger<VinylVaultContentTypes> _logger;

    // Well-known Umbraco built-in data type GUIDs (stable across all Umbraco v14+ installs)
    private static readonly Guid BuiltInTextBoxKey = new("0cc0eba1-9960-42c9-bf9b-60e150b429ae");
    private static readonly Guid BuiltInTextAreaKey = new("c6bac0dd-4ab9-45b1-8e30-e4b619ee5da3");
    private static readonly Guid BuiltInTrueFalseKey = new("92897bc6-a5f3-4ffe-ae27-f2e7e33dda49");
    private static readonly Guid BuiltInNumericKey = new("2e6d3631-066e-44b8-aec4-96f09099b2b5");
    private static readonly Guid BuiltInRichTextEditorKey = new("ca90c950-0aff-4e72-b976-a30b1ac57dad");
    private static readonly Guid BuiltInMediaPicker3Key = new("1df9f033-e6d4-451f-b8d2-e0cbc50a836f");

    public VinylVaultContentTypes(
        IContentTypeService contentTypeService,
        IDataTypeService dataTypeService,
        IShortStringHelper shortStringHelper,
        IWebHostEnvironment env,
        IRuntimeState runtimeState,
        ILogger<VinylVaultContentTypes> logger)
    {
        _contentTypeService = contentTypeService;
        _dataTypeService = dataTypeService;
        _shortStringHelper = shortStringHelper;
        _env = env;
        _runtimeState = runtimeState;
        _logger = logger;
    }

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (_runtimeState.Level < RuntimeLevel.Run) return;
        if (!_env.IsDevelopment()) return;

        _logger.LogInformation("VINYL VAULT SCHEMA: Starting setup");
        await Task.Run(SetupSchemaAsync, cancellationToken);
        _logger.LogInformation("VINYL VAULT SCHEMA: Setup complete");
    }

    private async Task SetupSchemaAsync()
    {
        try
        {
            // Check if already exists
            if (_contentTypeService.Get("vinylVaultHome") != null)
            {
                _logger.LogDebug("VINYL VAULT SCHEMA: Content types already exist");
                return;
            }

            // Get built-in data types
            var textBox = await _dataTypeService.GetAsync(BuiltInTextBoxKey);
            var textArea = await _dataTypeService.GetAsync(BuiltInTextAreaKey);
            var trueFalse = await _dataTypeService.GetAsync(BuiltInTrueFalseKey);
            var numeric = await _dataTypeService.GetAsync(BuiltInNumericKey);
            var richText = await _dataTypeService.GetAsync(BuiltInRichTextEditorKey);
            var mediaPicker = await _dataTypeService.GetAsync(BuiltInMediaPicker3Key);

            if (textBox == null || textArea == null || trueFalse == null || numeric == null || richText == null || mediaPicker == null)
            {
                _logger.LogError("VINYL VAULT SCHEMA: Could not resolve built-in data types");
                return;
            }

            // Create content types in order
            await CreateVinylVaultHomeTypeAsync(textBox, textArea);
            await CreateVinylGenreLandingTypeAsync(textBox, textArea);
            await CreateVinylRecordTypeAsync(textBox, textArea, trueFalse, numeric, richText, mediaPicker);

            _logger.LogInformation("VINYL VAULT SCHEMA: All content types created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "VINYL VAULT SCHEMA: Unexpected error during setup");
        }
    }

    private async Task CreateVinylVaultHomeTypeAsync(IDataType textBox, IDataType textArea)
    {
        const string alias = "vinylVaultHome";
        if (_contentTypeService.Get(alias) != null) return;

        var contentType = new ContentType(_shortStringHelper, -1)
        {
            Alias = alias,
            Name = "Vinyl Vault Home",
            Icon = "icon-store",
            AllowedAsRoot = false,
            IsElement = false,
            Description = "Vinyl Vault shop landing page"
        };

        const string groupName = "Content";
        contentType.AddPropertyGroup(groupName, groupName.ToLower().Replace(" ", ""));

        contentType.AddPropertyType(new PropertyType(_shortStringHelper, textBox, "heroTitle")
        {
            Name = "Hero Title",
            Description = "Main hero heading",
            Mandatory = false
        }, groupName);

        contentType.AddPropertyType(new PropertyType(_shortStringHelper, textArea, "heroSubtitle")
        {
            Name = "Hero Subtitle",
            Description = "Tagline or intro text",
            Mandatory = false
        }, groupName);

#pragma warning disable CS0618
        _contentTypeService.Save(contentType);
#pragma warning restore CS0618

        _logger.LogInformation("VINYL VAULT SCHEMA: Created vinylVaultHome content type");
        await Task.CompletedTask;
    }

    private async Task CreateVinylGenreLandingTypeAsync(IDataType textBox, IDataType textArea)
    {
        const string alias = "vinylGenreLanding";
        if (_contentTypeService.Get(alias) != null) return;

        var contentType = new ContentType(_shortStringHelper, -1)
        {
            Alias = alias,
            Name = "Vinyl Genre Landing",
            Icon = "icon-folder-music",
            AllowedAsRoot = false,
            IsElement = false,
            Description = "Genre category landing page"
        };

        const string groupName = "Content";
        contentType.AddPropertyGroup(groupName, groupName.ToLower().Replace(" ", ""));

        contentType.AddPropertyType(new PropertyType(_shortStringHelper, textBox, "genre")
        {
            Name = "Genre",
            Description = "Genre name (Jazz, Rock, Electronic, etc.)",
            Mandatory = true
        }, groupName);

        contentType.AddPropertyType(new PropertyType(_shortStringHelper, textArea, "description")
        {
            Name = "Description",
            Description = "Genre description for landing page",
            Mandatory = false
        }, groupName);

#pragma warning disable CS0618
        _contentTypeService.Save(contentType);
#pragma warning restore CS0618

        _logger.LogInformation("VINYL VAULT SCHEMA: Created vinylGenreLanding content type");
        await Task.CompletedTask;
    }

    private async Task CreateVinylRecordTypeAsync(
        IDataType textBox,
        IDataType textArea,
        IDataType trueFalse,
        IDataType numeric,
        IDataType richText,
        IDataType mediaPicker)
    {
        const string alias = "vinylRecord";
        if (_contentTypeService.Get(alias) != null) return;

        var contentType = new ContentType(_shortStringHelper, -1)
        {
            Alias = alias,
            Name = "Vinyl Record",
            Icon = "icon-vinyl",
            AllowedAsRoot = false,
            IsElement = false,
            Description = "Individual vinyl record content node"
        };

        // Content tab
        const string contentGroup = "Content";
        contentType.AddPropertyGroup(contentGroup, "content");

        contentType.AddPropertyType(new PropertyType(_shortStringHelper, textBox, "title")
        {
            Name = "Title",
            Description = "Album title",
            Mandatory = true
        }, contentGroup);

        contentType.AddPropertyType(new PropertyType(_shortStringHelper, textBox, "artist")
        {
            Name = "Artist",
            Description = "Artist or band name",
            Mandatory = true
        }, contentGroup);

        contentType.AddPropertyType(new PropertyType(_shortStringHelper, textBox, "genre")
        {
            Name = "Genre",
            Description = "Genre (Jazz, Rock, Electronic, Hip-Hop, Classical, Techno, Nose Flute Jazz)",
            Mandatory = true
        }, contentGroup);

        contentType.AddPropertyType(new PropertyType(_shortStringHelper, numeric, "releaseYear")
        {
            Name = "Release Year",
            Description = "Original release year",
            Mandatory = false
        }, contentGroup);

        contentType.AddPropertyType(new PropertyType(_shortStringHelper, richText, "description")
        {
            Name = "Description",
            Description = "Album description, track listing, history",
            Mandatory = false
        }, contentGroup);

        contentType.AddPropertyType(new PropertyType(_shortStringHelper, mediaPicker, "coverImage")
        {
            Name = "Cover Image",
            Description = "Album cover art",
            Mandatory = false
        }, contentGroup);

        // Inventory tab
        const string inventoryGroup = "Inventory";
        contentType.AddPropertyGroup(inventoryGroup, "inventory");

        contentType.AddPropertyType(new PropertyType(_shortStringHelper, trueFalse, "inStock")
        {
            Name = "In Stock",
            Description = "Is this vinyl currently available?",
            Mandatory = false
        }, inventoryGroup);

        contentType.AddPropertyType(new PropertyType(_shortStringHelper, numeric, "stockCount")
        {
            Name = "Stock Count",
            Description = "Number of copies in stock",
            Mandatory = false
        }, inventoryGroup);

        contentType.AddPropertyType(new PropertyType(_shortStringHelper, trueFalse, "isLimitedEdition")
        {
            Name = "Limited Edition",
            Description = "Is this a limited edition drop?",
            Mandatory = false
        }, inventoryGroup);

        // Notifications tab
        const string notificationsGroup = "Notifications";
        contentType.AddPropertyGroup(notificationsGroup, "notifications");

        contentType.AddPropertyType(new PropertyType(_shortStringHelper, textBox, "notificationGenre")
        {
            Name = "Notification Genre",
            Description = "Genre value for notification routing (must match genre exactly)",
            Mandatory = false
        }, notificationsGroup);

#pragma warning disable CS0618
        _contentTypeService.Save(contentType);
#pragma warning restore CS0618

        _logger.LogInformation("VINYL VAULT SCHEMA: Created vinylRecord content type");

        // Now update parent types to allow these children
        var vinylGenreLanding = _contentTypeService.Get("vinylGenreLanding");
        if (vinylGenreLanding != null)
        {
            vinylGenreLanding.AllowedContentTypes = new[]
            {
                new ContentTypeSort(contentType.Key, 0, contentType.Alias)
            };
#pragma warning disable CS0618
            _contentTypeService.Save(vinylGenreLanding);
#pragma warning restore CS0618
        }

        var vinylVaultHome = _contentTypeService.Get("vinylVaultHome");
        if (vinylVaultHome != null && vinylGenreLanding != null)
        {
            vinylVaultHome.AllowedContentTypes = new[]
            {
                new ContentTypeSort(vinylGenreLanding.Key, 0, vinylGenreLanding.Alias)
            };
#pragma warning disable CS0618
            _contentTypeService.Save(vinylVaultHome);
#pragma warning restore CS0618
        }

        await Task.CompletedTask;
    }
}
