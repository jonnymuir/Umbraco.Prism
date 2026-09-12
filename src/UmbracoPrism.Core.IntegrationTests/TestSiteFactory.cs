using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UmbracoPrism.Core.IntegrationTests;

/// <summary>
/// Every booted-host test class shares one <see cref="TestSiteFactory"/> — Umbraco is expensive
/// to boot and Wayfinder's static SupportSystemRegistry can only be registered once per process,
/// so a second WebApplicationFactory boot in the same process would fail.
/// </summary>
[CollectionDefinition(Name)]
public sealed class BootedTestSite : ICollectionFixture<TestSiteFactory>
{
    public const string Name = "Booted TestSite";
}

/// <summary>
/// Boots the real <c>UmbracoPrism.TestSite</c> host — full Umbraco, Prism's composer (the
/// PrismMemberCookie / backoffice schemes, every policy), and its own Wayfinder.Umbraco-backed
/// demo queue — for the authorization-contract behavioural suite (auth-contract Layer 2).
///
/// Everything Umbraco / Prism would otherwise write into the source tree on first boot is
/// redirected to a per-run temp directory (a fresh SQLite DB, the models directory) and
/// unattended-install is forced on via config, so no wizard and no source-tree writes. OIDC is
/// left unconfigured for connectivity — the deny-path tests never trigger a real challenge.
/// </summary>
public sealed class TestSiteFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(), "prism-authcontract-" + Guid.NewGuid().ToString("N"));
    private readonly HostErrorLogCapture _errorLogCapture = new();
    private readonly RawExceptionCapture _rawExceptionCapture = new();

    /// <summary>
    /// Drains every Error/Critical-level log entry captured since the last drain — including any
    /// exception object attached — so a test can surface exactly what the host logged for an
    /// unexpected response (e.g. a 500 it didn't ask for), rather than just the status code.
    /// See AuthorizationBehaviourTests' BiometricController.Exchange test for why this exists: a
    /// CI-only 500 with no local repro, previously undiagnosable because nothing captured the
    /// actual exception.
    /// </summary>
    public IReadOnlyList<HostErrorLogCapture.Entry> DrainRecentErrorLogs() => _errorLogCapture.Drain();

    /// <summary>
    /// Drains every exception that unwound past any middleware in the pipeline since the last
    /// drain — see <see cref="RawExceptionCapture"/>. Stronger than <see cref="DrainRecentErrorLogs"/>:
    /// bypasses logging entirely, so it can't miss an exception that's logged below Error level
    /// or not logged at all.
    /// </summary>
    public IReadOnlyList<Exception> DrainRawExceptions() => _rawExceptionCapture.Drain();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, "umbraco", "Data"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "umbraco", "models"));

        builder.UseEnvironment(Environments.Development);
        builder.ConfigureLogging(logging => logging.AddProvider(_errorLogCapture));
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter>(_rawExceptionCapture));

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:umbracoDbDSN"] =
                    $"Data Source={Path.Combine(_tempRoot, "umbraco", "Data", "Umbraco.sqlite.db")};Cache=Shared;Foreign Keys=True;Pooling=True",
                ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite",

                // Supplied so Umbraco never generates one and persists it back into the source
                // appsettings.json. A fixed base64 blob; this host serves no images under test.
                ["Umbraco:CMS:Imaging:HMACSecretKey"] = "cHJpc20tYXV0aC1jb250cmFjdC1sYXllcjItbm90LXNlY3JldA==",

                // BiometricTokenService/RefreshTokenEncryptionService throw InvalidOperationException
                // from their own constructors — i.e. during controller DI activation, before any
                // middleware-level or action-level try/catch can reach it — when these are absent.
                // UmbracoPrism.TestSite normally gets them from `dotnet user-secrets` (its
                // UserSecretsId), which .NET auto-loads because this factory forces the Development
                // environment below; user secrets live outside the repo, so CI never has them. This
                // is THE actual root cause behind AuthorizationBehaviourTests' long-standing
                // "CI-only 500" note on the Exchange test — not a cold-runner timing flake at all
                // (it was 100% deterministic in CI from the start, just never actually diagnosed
                // until RawExceptionCapture's response-body capture finally showed the real
                // exception). Fixed test-only values, not real secrets — this host issues no real
                // biometric tokens under test.
                ["Prism:Biometric:SigningKey"] = "prism-authcontract-test-signing-key-not-a-real-secret",
                ["Prism:Biometric:EncryptionKey"] = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=",

                ["Umbraco:CMS:ModelsBuilder:ModelsDirectory"] = Path.Combine(_tempRoot, "umbraco", "models"),
                ["Umbraco:CMS:ModelsBuilder:AcceptUnsafeModelsDirectory"] = "true",

                ["Umbraco:CMS:Unattended:InstallUnattended"] = "true",
                ["Umbraco:CMS:Unattended:UpgradeUnattended"] = "true",
                ["Umbraco:CMS:Unattended:PackageMigrationsUnattended"] = "true",
                ["Umbraco:CMS:Unattended:UnattendedUserName"] = "Auth Contract",
                ["Umbraco:CMS:Unattended:UnattendedUserEmail"] = "auth-contract@example.test",
                ["Umbraco:CMS:Unattended:UnattendedUserPassword"] = "AuthContract123!",
            });
        });
    }

    public async Task InitializeAsync()
    {
        // https:// — see the same note on AuthorizationBehaviourTests.Anonymous(): TestSite is
        // HTTPS-only in every real deployment, and AntiforgeryOptions.Cookie.SecurePolicy =
        // Always hard-throws on a non-HTTPS request.
        using var client = CreateClient(new()
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        var deadline = DateTime.UtcNow.AddMinutes(4);
        var ok = 0;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var res = await client.GetAsync("/umbraco");
                if (res.StatusCode is System.Net.HttpStatusCode.OK or System.Net.HttpStatusCode.Redirect)
                {
                    if (++ok >= 3)
                    {
                        return;
                    }
                }
                else
                {
                    ok = 0;
                }
            }
            catch (HttpRequestException)
            {
                ok = 0;
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException("UmbracoPrism.TestSite did not reach a ready state within 4 minutes.");
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_tempRoot))
        {
            try
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
            catch (IOException)
            {
                // A SQLite handle can linger a moment after shutdown; %TEMP% is reaped anyway.
            }
        }
    }
}
