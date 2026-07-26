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
/// Creates the <c>guidanceArticle</c> document type on application startup — real, independently
/// editable CMS content that the "Transfer a Professional Juggling Licence" CMS Service Blueprint demo's
/// <c>guidance-checklist</c> component links to. Seeded ahead of the service blueprint itself (which is
/// authored live via the CMS Service Blueprint MCP in a separate recorded walkthrough) so that build has
/// real content to reference rather than needing to author it on camera.
/// </summary>
public class GuidanceArticleContentTypes(
    IContentTypeService contentTypeService,
    IDataTypeService dataTypeService,
    IShortStringHelper shortStringHelper,
    ITemplateService templateService,
    IWebHostEnvironment env,
    IRuntimeState runtimeState,
    ILogger<GuidanceArticleContentTypes> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    public const string Alias = "guidanceArticle";

    // Well-known Umbraco built-in data type GUID (stable across all Umbraco v14+ installs).
    private static readonly Guid BuiltInRichTextEditorKey = new("ca90c950-0aff-4e72-b976-a30b1ac57dad");

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (runtimeState.Level < RuntimeLevel.Run) return;
        if (!env.IsDevelopment()) return;

        try
        {
            await SetupSchemaAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GUIDANCE ARTICLE SCHEMA: Unexpected error during setup");
        }
    }

    private async Task SetupSchemaAsync()
    {
        var contentType = contentTypeService.Get(Alias);
        if (contentType != null)
        {
            logger.LogDebug("GUIDANCE ARTICLE SCHEMA: Content type already exists");
            await EnsureTemplateAsync(contentType);
            AllowUnderHomePage(contentType);
            return;
        }

        var richText = await dataTypeService.GetAsync(BuiltInRichTextEditorKey);
        if (richText == null)
        {
            logger.LogError("GUIDANCE ARTICLE SCHEMA: Could not resolve built-in rich text editor data type");
            return;
        }

        contentType = new ContentType(shortStringHelper, -1)
        {
            Alias = Alias,
            Name = "Guidance Article",
            Icon = "icon-book-alt",
            AllowedAsRoot = false,
            IsElement = false,
            Description = "A single CMS-managed guidance article, linked to from a service blueprint's guidance-checklist component."
        };

        const string groupName = "Content";
        contentType.AddPropertyGroup(groupName, "content");

        contentType.AddPropertyType(new PropertyType(shortStringHelper, richText, "body")
        {
            Name = "Body",
            Description = "The guidance article's content.",
            Mandatory = false
        }, groupName);

#pragma warning disable CS0618
        contentTypeService.Save(contentType);
#pragma warning restore CS0618

        logger.LogInformation("GUIDANCE ARTICLE SCHEMA: Created guidanceArticle content type");

        await EnsureTemplateAsync(contentType);
        AllowUnderHomePage(contentType);
    }

    /// <summary>
    /// A content type needs a real <see cref="Template"/> entity assigned — not just a matching
    /// <c>Views/{Alias}.cshtml</c> file — for Umbraco to route published content of that type at
    /// all; mirrors <c>PrismContentTypeSeeder.EnsureTemplateAsync</c>'s exact approach.
    /// </summary>
    private async Task EnsureTemplateAsync(IContentType contentType)
    {
        if (contentType.AllowedTemplates?.Any() == true) return;

        var template = await templateService.GetAsync(contentType.Alias);
        if (template == null)
        {
            var attempt = await templateService.CreateForContentTypeAsync(
                "Guidance Article", contentType.Alias, contentType.Alias, Constants.Security.SuperUserKey);
            template = attempt.Result;
        }

        if (template == null)
        {
            logger.LogWarning("GUIDANCE ARTICLE SCHEMA: Could not create a template for guidanceArticle");
            return;
        }

        contentType.AllowedTemplates = [template];
        contentType.SetDefaultTemplate(template);
#pragma warning disable CS0618
        contentTypeService.Save(contentType);
#pragma warning restore CS0618

        logger.LogInformation("GUIDANCE ARTICLE SCHEMA: Template assigned to guidanceArticle");
    }

    /// <summary>
    /// Adds <c>guidanceArticle</c> to homePage's allowed children — appended, never replacing
    /// the list Core's own <c>PrismContentTypeSeeder</c> already set there.
    /// </summary>
    private void AllowUnderHomePage(IContentType guidanceArticleType)
    {
        var homePage = contentTypeService.Get("homePage");
        if (homePage == null)
        {
            logger.LogDebug("GUIDANCE ARTICLE SCHEMA: homePage content type not found; skipping allowed-children update");
            return;
        }

        if ((homePage.AllowedContentTypes ?? []).Any(sort => sort.Alias == Alias))
        {
            return;
        }

        var existingChildren = homePage.AllowedContentTypes ?? [];
        homePage.AllowedContentTypes = existingChildren
            .Append(new ContentTypeSort(guidanceArticleType.Key, existingChildren.Count(), Alias));

#pragma warning disable CS0618
        contentTypeService.Save(homePage);
#pragma warning restore CS0618

        logger.LogInformation("GUIDANCE ARTICLE SCHEMA: homePage now allows guidanceArticle as a child");
    }
}
