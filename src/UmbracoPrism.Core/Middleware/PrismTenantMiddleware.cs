using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Middleware;

/// <summary>
/// Resolves the current tenant from the incoming host and stores it in the Prism request context.
/// </summary>
/// <param name="next">The next middleware delegate in the pipeline.</param>
/// <param name="logger">Logger used for unknown-host and resolution diagnostics.</param>
public class PrismTenantMiddleware(RequestDelegate next, ILogger<PrismTenantMiddleware> logger)
{
    /// <summary>
    /// Resolves tenant context for the current request and warms tenant signing keys when available.
    /// </summary>
    /// <param name="context">The current HTTP request context.</param>
    /// <param name="tenantService">Service used to map request host names to Prism tenants.</param>
    /// <param name="prismContext">Scoped Prism context where the resolved tenant is stored.</param>
    /// <param name="signingKeyCache">Signing key cache pre-warmed for the resolved tenant.</param>
    /// <returns>A task that completes after tenant resolution and downstream middleware execution.</returns>
    public async Task InvokeAsync(HttpContext context, ITenantService tenantService, IPrismContext prismContext, IPrismSigningKeyCache signingKeyCache)
    {
        var host = context.Request.Host.Host;
        
        // Attempt to resolve tenant
        var tenant = await tenantService.GetByDomainAsync(host);

        if (tenant != null)
        {
            prismContext.CurrentTenant = tenant;

            if (!string.IsNullOrEmpty(tenant.EntraTenantId))
            {
                try
                {
                    await signingKeyCache.WarmAsync(tenant.EntraTenantId, cancellationToken: context.RequestAborted);
                }
                catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to warm Prism signing keys for tenant '{TenantId}'. Continuing request with existing cache state.", tenant.EntraTenantId);
                }
            }
        }
        else
        {
            // Optional: Handle unknown tenant (redirect or default)
            logger.LogWarning("Unknown tenant domain: {Host}", host);
        }

        await next(context);
    }
}