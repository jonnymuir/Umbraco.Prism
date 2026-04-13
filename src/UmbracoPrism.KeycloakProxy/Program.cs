var builder = WebApplication.CreateBuilder(args);

// Use the .NET dev certificate for HTTPS on localhost:8443.
// This certificate is already trusted on most dev machines via `dotnet dev-certs https --trust`.
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.ListenLocalhost(8443, listenOptions =>
    {
        listenOptions.UseHttps();
    });
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.MapReverseProxy();

app.Run();
