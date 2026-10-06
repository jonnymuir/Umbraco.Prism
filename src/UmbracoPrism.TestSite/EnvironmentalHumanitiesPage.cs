using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.TestSite;

/// <summary>
/// The Environmental humanities hub: an ordinary rich-text page under Home that describes the
/// field recording services and links to each one. Ensures the <c>environmentalHumanitiesPage</c>
/// document type, then seeds the page once. Editors own the body from there, so a new service is
/// a link added in the backoffice, not a code change.
/// </summary>
public class EnvironmentalHumanitiesPage(
    IContentService contentService,
    IContentTypeService contentTypeService,
    IDataTypeService dataTypeService,
    ITemplateService templateService,
    IShortStringHelper shortStringHelper,
    IOptions<PrismConfiguration> prismConfig,
    IRuntimeState runtimeState,
    ILogger<EnvironmentalHumanitiesPage> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    // Umbraco's built-in Richtext editor data type, stable across v14+ installs.
    private static readonly Guid BuiltInRichTextEditorKey = new("ca90c950-0aff-4e72-b976-a30b1ac57dad");

    private const string BodyAlias = "body";

    private static readonly string BodyMarkup = $"""
        <p>Environmental humanities brings the humanities and the sciences together to ask how people notice, record and care for the places they live in. These services are reference examples of a practitioner collecting evidence in the field with nothing but a phone, and of what a service can do with it afterwards.</p>
        <h2>Services</h2>
        <ul>
        <li><a href="{TestSiteSeedContract.ButterflySightingPageUrl}">{TestSiteSeedContract.ButterflySightingPageName}</a>: photograph a butterfly and let the phone supply the place and time. An AI agent suggests the species, you confirm or correct it, and the sighting is recorded.</li>
        </ul>
        <h2>How the examples work</h2>
        <p>Each service is a Wayfinder service blueprint. The AI step runs as an Umbraco Automate automation calling an Umbraco.AI agent, and the practitioner always has the last word.</p>
        """;

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (runtimeState.Level < RuntimeLevel.Run) return;
        if (!prismConfig.Value.SeedStarterContent) return;

        try
        {
            var contentType = await EnsureContentTypeAsync();
            await EnsureTemplateAsync(contentType);
            EnsurePage(contentType);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ENVIRONMENTAL HUMANITIES: Unexpected error; skipping");
        }
    }

    private async Task<IContentType> EnsureContentTypeAsync()
    {
        var existing = contentTypeService.Get(TestSiteSeedContract.EnvironmentalHumanitiesPageAlias);
        if (existing is not null) return existing;

        var richText = await dataTypeService.GetAsync(BuiltInRichTextEditorKey)
            ?? throw new InvalidOperationException("The built-in Richtext editor data type was not found.");

        var contentType = new ContentType(shortStringHelper, -1)
        {
            Alias = TestSiteSeedContract.EnvironmentalHumanitiesPageAlias,
            Name = "Environmental Humanities Page",
            Icon = "icon-leaf",
            AllowedAsRoot = false,
            Description = "A rich-text hub describing the field recording services and linking to each one"
        };
        contentType.AddPropertyGroup("Content", "content");
        contentType.AddPropertyType(new PropertyType(shortStringHelper, richText, BodyAlias)
        {
            Name = "Body",
            Description = "Describe the services and link to each one.",
            SortOrder = 0
        }, "Content");

#pragma warning disable CS0618
        contentTypeService.Save(contentType);
#pragma warning restore CS0618

        AllowUnderHome(contentType);
        logger.LogInformation("ENVIRONMENTAL HUMANITIES: document type created");
        return contentType;
    }

    private void AllowUnderHome(IContentType contentType)
    {
        var home = contentTypeService.Get(TestSiteSeedContract.HomePageAlias);
        if (home is null) return;

        var allowed = (home.AllowedContentTypes ?? []).ToList();
        if (allowed.Any(sort => sort.Alias == contentType.Alias)) return;

        allowed.Add(new ContentTypeSort(contentType.Key, allowed.Count, contentType.Alias));
        home.AllowedContentTypes = allowed;
#pragma warning disable CS0618
        contentTypeService.Save(home);
#pragma warning restore CS0618
    }

    private async Task EnsureTemplateAsync(IContentType contentType)
    {
        if (contentType.AllowedTemplates?.Any() == true) return;

        var template = await templateService.GetAsync(contentType.Alias);
        if (template is null)
        {
            var attempt = await templateService.CreateForContentTypeAsync(
                contentType.Name!, contentType.Alias, contentType.Alias, Constants.Security.SuperUserKey);
            template = attempt.Result;
        }

        if (template is null)
        {
            logger.LogWarning("ENVIRONMENTAL HUMANITIES: Could not create a template");
            return;
        }

        contentType.AllowedTemplates = [template];
        contentType.SetDefaultTemplate(template);
#pragma warning disable CS0618
        contentTypeService.Save(contentType);
#pragma warning restore CS0618
    }

    private void EnsurePage(IContentType contentType)
    {
        if (TestSiteSeedContract.FindContentByAlias(contentService, contentType.Alias) is not null) return;

        var home = TestSiteSeedContract.FindContentByAlias(contentService, TestSiteSeedContract.HomePageAlias);
        if (home is null)
        {
            logger.LogDebug("ENVIRONMENTAL HUMANITIES: homePage not found; the page is created on a later boot");
            return;
        }

        var page = contentService.Create(TestSiteSeedContract.EnvironmentalHumanitiesPageName, home.Id, contentType.Alias);
        page.SetValue(BodyAlias, RichTextValue(BodyMarkup));

        var save = contentService.Save(page);
        if (!save.Success)
        {
            logger.LogWarning("ENVIRONMENTAL HUMANITIES: Save failed: {Reason}", save.Result);
            return;
        }

#pragma warning disable CS0618
        var publish = contentService.Publish(page, ["*"], Constants.Security.SuperUserId);
#pragma warning restore CS0618
        if (publish.Success)
        {
            logger.LogInformation("ENVIRONMENTAL HUMANITIES: page created and published");
        }
        else
        {
            logger.LogWarning("ENVIRONMENTAL HUMANITIES: Publish failed: {Reason}", publish.Result);
        }
    }

    /// <summary>The persisted <c>Umbraco.RichText</c> value: markup plus an empty block set.</summary>
    private static string RichTextValue(string markup) => JsonSerializer.Serialize(new
    {
        markup,
        blocks = new
        {
            layout = new { },
            contentData = Array.Empty<object>(),
            settingsData = Array.Empty<object>(),
            expose = Array.Empty<object>()
        }
    }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
}
