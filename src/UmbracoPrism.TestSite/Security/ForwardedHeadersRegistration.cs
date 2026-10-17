using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Web.Common.ApplicationBuilder;

namespace UmbracoPrism.TestSite.Security;

/// <summary>
/// The reference site's proxy trust: X-Forwarded-For/X-Forwarded-Proto from the proxies named in
/// <see cref="ForwardedHeadersTrustOptions"/>, and from loopback (ASP.NET Core's own default)
/// otherwise. Only the host knows what sits in front of it, which is why this lives here rather
/// than in the Prism package.
/// </summary>
public static class ForwardedHeadersRegistration
{
    public static IUmbracoBuilder AddTrustedForwardedHeaders(this IUmbracoBuilder builder)
    {
        builder.Services.Configure<ForwardedHeadersTrustOptions>(
            builder.Config.GetSection(ForwardedHeadersTrustOptions.SectionName));
        builder.Services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<ForwardedHeadersTrustOptions>>((options, trust) => trust.Value.ApplyTo(options));

        // First of all the pipeline filters: RemoteIpAddress (the biometric-exchange rate limiter)
        // and the request scheme (HSTS, OIDC redirect URIs) must be the client's before anything
        // reads them.
        builder.Services.Configure<UmbracoPipelineOptions>(options =>
            options.PipelineFilters.Insert(0, new UmbracoPipelineFilter(
                "TestSiteForwardedHeaders",
                app => app.UseForwardedHeaders())));

        return builder;
    }
}
