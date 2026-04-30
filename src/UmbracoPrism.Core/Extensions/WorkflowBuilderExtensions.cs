using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using UmbracoPrism.Core.Configuration;
using UmbracoPrism.Core.Services;
using UmbracoPrism.Core.Services.Sanitization;
using UmbracoPrism.Shared.Services.Sanitization;

namespace UmbracoPrism.Core.Extensions;

/// <summary>
/// Extension methods for registering the Prism Workflow Engine services.
/// </summary>
public static class WorkflowBuilderExtensions
{
    /// <summary>
    /// Registers the Prism Workflow Engine services with the DI container.
    /// This is the primary integration point for the new Business App-centric workflow architecture.
    /// </summary>
    /// <param name="builder">The Umbraco builder.</param>
    /// <returns>The Umbraco builder for method chaining.</returns>
    /// <remarks>
    /// Registers:
    /// - <see cref="IBusinessAppWorkflowClient"/> (scoped) — HTTP client for calling the Business App
    /// - <see cref="IWorkflowStepNonceService"/> (singleton) — Nonce generation and validation for tamper-proof forms
    /// - <see cref="IDistributedCache"/> (singleton) — In-memory cache (replace with Redis/SQL for multi-server)
    /// - <see cref="PrismWorkflowOptions"/> — Configuration from "Prism:Workflow" section
    /// 
    /// The Business App is the authoritative source for all workflow state and definitions.
    /// Umbraco uses this client to ask "what's the next step?" and to submit collected data.
    /// 
    /// In development, configure <c>PrismBusinessApp:WorkflowApiBaseUrl</c> to the local HTTPS endpoint
    /// (e.g. <c>https://localhost:7245</c>) so browser and server-side flows share the same trusted origin.
    /// </remarks>
    public static IUmbracoBuilder AddPrismWorkflowEngine(this IUmbracoBuilder builder)
    {
        builder.Services.AddHttpClient("PrismBusinessApp");
        builder.Services.AddScoped<IBusinessAppWorkflowClient, BusinessAppWorkflowClient>();

        // Distributed cache — works out of the box for single-server dev.
        // Replace with AddStackExchangeRedisCache() or AddDistributedSqlServerCache() for multi-server production.
        builder.Services.AddDistributedMemoryCache();

        // Workflow configuration options
        builder.Services.Configure<PrismWorkflowOptions>(
            builder.Config.GetSection("Prism:Workflow"));

        // Workflow nonce service for tamper-proof form submission
        builder.Services.AddSingleton<IWorkflowStepNonceService, WorkflowStepNonceService>();

        // Workflow field validator for server-side structural validation
        builder.Services.AddTransient<IWorkflowFieldValidator, WorkflowFieldValidator>();

        // Content sanitizer — NoOp placeholder until Copper's SEC-003 T2 real impl lands.
        // Copper will replace NoOpWorkflowContentSanitizer with WorkflowContentSanitizer
        // (Ganss.Xss-backed GDS allowlist) and re-register here.
        builder.Services.AddSingleton<IWorkflowContentSanitizer, NoOpWorkflowContentSanitizer>();

        return builder;
    }
}
