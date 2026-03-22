using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Middleware;

/// <summary>
/// Middleware to resolve tenant based on incoming request. The brain that checks the domain.
/// </summary>
public class PrismTenantMiddleware(RequestDelegate next, ILogger<PrismTenantMiddleware> logger)
{
    /// <summary>
    /// InvokeAsync method to handle tenant resolution.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="tenantService"></param>
    /// <param name="prismContext"></param>
    /// <returns></returns>
    public async Task InvokeAsync(HttpContext context, ITenantService tenantService, IPrismContext prismContext, IPrismSigningKeyCache signingKeyCache)
    {
        var host = context.Request.Host.Host;
        
        // Attempt to resolve tenant
        var tenant = await tenantService.GetByDomainAsync(host);

        if (tenant != null)
        {
            prismContext.CurrentTenant = tenant;

            if (!string.IsNullOrEmpty(tenant.EntraTenantId))
                await signingKeyCache.WarmAsync(tenant.EntraTenantId, context.RequestAborted);
        }
        else
        {
            // Optional: Handle unknown tenant (redirect or default)
            logger.LogWarning($"Unknown tenant domain: {host}");
        }

        await next(context);
    }
}