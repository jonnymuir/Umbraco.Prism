using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace UmbracoPrism.TestSite;

/// <summary>
/// Seeds the four guidance articles referenced by the (separately, live-authored) "Transfer a
/// Professional Juggling Licence" CMS Service Blueprint demo's guidance-checklist component. Runs after
/// <see cref="GuidanceArticleContentTypes"/> has created the <c>guidanceArticle</c> content type.
/// Idempotent — skips if the articles already exist. Development-only, like every other
/// TestSite demo seeder.
/// </summary>
public class GuidanceArticleSeeder(
    IContentService contentService,
    IWebHostEnvironment env,
    IRuntimeState runtimeState,
    ILogger<GuidanceArticleSeeder> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private static readonly (string Name, string Body)[] Articles =
    [
        ("Transfer Rules",
            "<p>Your existing professional juggling licence can be transferred to the National " +
            "Juggling Authority if it was issued by a recognised juggling authority and remains " +
            "in good standing.</p>"),
        ("International Transfers",
            "<p>If your licence was issued outside the United Kingdom, your overseas juggling " +
            "authority must be recognised by the International Juggling Accreditation Register " +
            "for your transfer to be eligible.</p>"),
        ("Supporting Evidence",
            "<p>You'll need to provide your current licence, proof of identity, proof of address, " +
            "and a professional juggling portfolio. Video evidence of your routines is optional " +
            "but can help support your application.</p>"),
        ("Professional Standards",
            "<p>The National Juggling Authority expects the same professional standards from " +
            "transferring jugglers as from those trained domestically — including safe handling " +
            "of flaming, bladed, and otherwise hazardous props.</p>"),
    ];

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (runtimeState.Level < RuntimeLevel.Run) return;
        if (!env.IsDevelopment()) return;

        try
        {
            await Task.Run(SeedContent, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GUIDANCE ARTICLE SEEDER: Unexpected error — safe to ignore");
        }
    }

    private void SeedContent()
    {
        var homePage = TestSiteSeedContract.FindContentByAlias(contentService, TestSiteSeedContract.HomePageAlias);
        if (homePage == null)
        {
            logger.LogDebug("GUIDANCE ARTICLE SEEDER: homePage not found; skipping");
            return;
        }

        var existingNames = TestSiteSeedContract
            .FindContentByAlias(contentService, GuidanceArticleContentTypes.Alias) != null;
        if (existingNames)
        {
            logger.LogDebug("GUIDANCE ARTICLE SEEDER: Articles already exist; skipping");
            return;
        }

        foreach (var (name, body) in Articles)
        {
            var article = contentService.Create(name, homePage.Id, GuidanceArticleContentTypes.Alias);
            article.SetValue("body", body);

#pragma warning disable CS0618
            contentService.Save(article);
            contentService.Publish(article, Array.Empty<string>(), Constants.Security.SuperUserId);
#pragma warning restore CS0618
        }

        logger.LogInformation("GUIDANCE ARTICLE SEEDER: Created {Count} guidance articles", Articles.Length);
    }
}
