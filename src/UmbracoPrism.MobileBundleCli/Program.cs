using UmbracoPrism.Core.Controllers.Models;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.MobileBundleCli;

/// <summary>
/// Headless equivalent of the Umbraco backoffice's "Produce Mobile" action.
///
/// <see cref="MobileBundleService.BuildBundleAsync"/> has zero DI dependencies — it only needs a
/// <see cref="PrismTenantSchema"/> and a <see cref="PrismMobileBundleRequest"/>, both plain POCOs
/// — so this CLI never boots Umbraco, a database, or Aspire. It exists so a CI pipeline (GitHub
/// Actions on macos-latest) can generate the same Capacitor starter bundle a backoffice admin
/// would download by hand, non-interactively, then unzip it and run its own bootstrap scripts.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        Dictionary<string, string> flags;
        try
        {
            flags = ParseFlags(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            PrintUsage();
            return 1;
        }

        if (!flags.TryGetValue("output", out var outputPath) || string.IsNullOrWhiteSpace(outputPath))
        {
            Console.Error.WriteLine("error: --output <path> is required");
            return 1;
        }

        if (!flags.TryGetValue("hostname", out var hostname) || string.IsNullOrWhiteSpace(hostname))
        {
            // MobileBundleService only needs a hostname to derive a start URL when --start-url
            // isn't given; if --start-url IS given, the tenant hostname is otherwise unused.
            if (!flags.ContainsKey("start-url"))
            {
                Console.Error.WriteLine("error: --hostname <host> (or --start-url <url>) is required");
                return 1;
            }

            hostname = new Uri(flags["start-url"]).Host;
        }

        var tenant = new PrismTenantSchema
        {
            Name = flags.GetValueOrDefault("app-name", hostname),
            Hostname = hostname,
            EntraTenantId = flags.GetValueOrDefault("entra-tenant-id")
        };

        var request = new PrismMobileBundleRequest
        {
            AppName = flags.GetValueOrDefault("app-name"),
            AppId = flags.GetValueOrDefault("app-id"),
            Version = flags.GetValueOrDefault("version"),
            StartUrl = flags.GetValueOrDefault("start-url"),
            UserAgentMarker = flags.GetValueOrDefault("user-agent-marker"),
            IconUrl = flags.GetValueOrDefault("icon-url"),
            SplashUrl = flags.GetValueOrDefault("splash-url"),
            BiometricAuthEnabled = ParseOptionalBool(flags.GetValueOrDefault("biometric-auth"))
        };

        try
        {
            var bundle = await new MobileBundleService().BuildBundleAsync(tenant, request);
            var fullOutputPath = Path.GetFullPath(outputPath);
            var directory = Path.GetDirectoryName(fullOutputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(fullOutputPath, bundle);
            Console.WriteLine($"Wrote mobile bundle ({bundle.Length:N0} bytes) to {fullOutputPath}");
            return 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static Dictionary<string, string> ParseFlags(string[] args)
    {
        var flags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"unexpected argument '{arg}' — flags must start with --");
            }

            var name = arg[2..];
            if (i + 1 >= args.Length)
            {
                throw new ArgumentException($"--{name} requires a value");
            }

            flags[name] = args[++i];
        }

        return flags;
    }

    private static bool? ParseOptionalBool(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : bool.Parse(value);

    private static void PrintUsage()
    {
        Console.WriteLine("""
            UmbracoPrism.MobileBundleCli — headless "Produce Mobile" bundle generation.

            Usage:
              dotnet run --project src/UmbracoPrism.MobileBundleCli -- --hostname <host> --output <path.zip> [options]

            Required (one of):
              --hostname <host>            Tenant hostname, e.g. prism-reference.example.com
              --start-url <url>            Full start URL, overrides --hostname; e.g. https://prism-reference.example.com

            Required:
              --output <path>              Where to write the generated bundle .zip

            Optional:
              --app-name <name>            Defaults to the hostname
              --app-id <reverse.dns.id>    Defaults to com.prism.<slugified-app-name>
              --version <semver>           Defaults to 1.0.0
              --user-agent-marker <text>   Defaults to PrismMobile
              --icon-url <url>
              --splash-url <url>
              --entra-tenant-id <guid>     Widens Capacitor's allowNavigation for that Entra tenant
              --biometric-auth <true|false>
            """);
    }
}
