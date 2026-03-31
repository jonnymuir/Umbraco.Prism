using Azure.Identity;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Load secrets from Azure Key Vault when a vault URI is configured.
// Secret names use '--' as the ':' separator, so 'Prism--Biometric--SigningKey'
// maps to the config key 'Prism:Biometric:SigningKey'.
// For local development, use .NET User Secrets instead of Key Vault
// (run: dotnet user-secrets set "Prism:Biometric:SigningKey" "<value>").
var vaultUri = builder.Configuration["Prism:VaultUri"];
if (!string.IsNullOrEmpty(vaultUri))
{
    builder.Configuration.AddAzureKeyVault(new Uri(vaultUri), new DefaultAzureCredential());
}

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

WebApplication app = builder.Build();

await app.BootUmbracoAsync();


app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();
