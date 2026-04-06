using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace UmbracoPrism.TestSite;

/// <summary>
/// Seeds Vinyl Vault demo content on application startup.
/// Creates genre landing pages and sample vinyl records per the design spec.
/// Idempotent — checks if content exists before creating.
/// Development-only for demo purposes.
/// </summary>
public class VinylVaultSeeder : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private readonly IContentService _contentService;
    private readonly IWebHostEnvironment _env;
    private readonly IRuntimeState _runtimeState;
    private readonly ILogger<VinylVaultSeeder> _logger;

    // Genre definitions from spec
    private static readonly string[] Genres =
    {
        "Jazz", "Rock", "Electronic", "Hip-Hop", "Classical", "Techno", "Nose Flute Jazz"
    };

    // Sample vinyl records per genre (3-4 per genre as specified)
    private static readonly Dictionary<string, List<VinylRecordSeed>> VinylsByGenre = new()
    {
        ["Jazz"] = new List<VinylRecordSeed>
        {
            new("Miles Davis - Kind of Blue", "Miles Davis", "Jazz", 1959, "A landmark album in jazz history, featuring modal jazz at its finest."),
            new("John Coltrane - A Love Supreme", "John Coltrane", "Jazz", 1965, "Spiritual jazz masterpiece in four movements."),
            new("Bill Evans - Portrait in Jazz", "Bill Evans", "Jazz", 1960, "Elegant trio recordings showcasing Evans' lyrical piano style."),
            new("Herbie Hancock - Head Hunters", "Herbie Hancock", "Jazz", 1973, "Funk-fusion classic that revolutionized jazz.")
        },
        ["Rock"] = new List<VinylRecordSeed>
        {
            new("Pink Floyd - The Dark Side of the Moon", "Pink Floyd", "Rock", 1973, "Progressive rock masterpiece exploring themes of madness and mortality."),
            new("Led Zeppelin - IV", "Led Zeppelin", "Rock", 1971, "Hard rock classic featuring Stairway to Heaven."),
            new("The Beatles - Abbey Road", "The Beatles", "Rock", 1969, "The Beatles' penultimate album, ending with the iconic medley."),
            new("The Who - Who's Next", "The Who", "Rock", 1971, "Power rock featuring synthesizers and anthemic songs.")
        },
        ["Electronic"] = new List<VinylRecordSeed>
        {
            new("Daft Punk - Random Access Memories", "Daft Punk", "Electronic", 2013, "Disco-influenced electronic masterpiece with live instrumentation."),
            new("Boards of Canada - Music Has the Right to Children", "Boards of Canada", "Electronic", 1998, "Nostalgic, warm electronica with analogue synthesis."),
            new("Aphex Twin - Selected Ambient Works 85-92", "Aphex Twin", "Electronic", 1992, "Pioneering ambient techno recordings."),
            new("Massive Attack - Blue Lines", "Massive Attack", "Electronic", 1991, "Trip-hop defining album blending hip-hop and electronica.")
        },
        ["Hip-Hop"] = new List<VinylRecordSeed>
        {
            new("Kendrick Lamar - To Pimp a Butterfly", "Kendrick Lamar", "Hip-Hop", 2015, "Genre-defying hip-hop exploring race, fame, and depression."),
            new("A Tribe Called Quest - The Low End Theory", "A Tribe Called Quest", "Hip-Hop", 1991, "Jazz-influenced hip-hop classic with tight production."),
            new("Wu-Tang Clan - Enter the Wu-Tang (36 Chambers)", "Wu-Tang Clan", "Hip-Hop", 1993, "Raw, gritty debut that changed hip-hop forever."),
            new("Nas - Illmatic", "Nas", "Hip-Hop", 1994, "East Coast hip-hop masterpiece with vivid storytelling.")
        },
        ["Classical"] = new List<VinylRecordSeed>
        {
            new("Beethoven - Symphony No. 9", "Ludwig van Beethoven", "Classical", 1824, "Choral symphony featuring the Ode to Joy."),
            new("Mozart - Requiem", "Wolfgang Amadeus Mozart", "Classical", 1791, "Hauntingly beautiful mass for the dead."),
            new("Bach - Goldberg Variations (Glenn Gould)", "Johann Sebastian Bach", "Classical", 1955, "Glenn Gould's legendary interpretation of Bach's keyboard work."),
            new("Vivaldi - The Four Seasons", "Antonio Vivaldi", "Classical", 1725, "Baroque violin concertos depicting the seasons.")
        },
        ["Techno"] = new List<VinylRecordSeed>
        {
            new("Kraftwerk - The Man-Machine", "Kraftwerk", "Techno", 1978, "Electronic music pioneers creating robotic soundscapes."),
            new("Jeff Mills - Exhibitionist", "Jeff Mills", "Techno", 2004, "Detroit techno legend's live mix compilation."),
            new("Richie Hawtin - DE9 | Closer to the Edit", "Richie Hawtin", "Techno", 2001, "Minimal techno DJ mix pushing boundaries."),
            new("Carl Craig - More Songs About Food and Revolutionary Art", "Carl Craig", "Techno", 1997, "Detroit techno with jazz and soul influences.")
        },
        ["Nose Flute Jazz"] = new List<VinylRecordSeed>
        {
            new("Various Artists - Nasal Passages: A Nose Flute Jazz Collection", "Various Artists", "Nose Flute Jazz", 2005, "The finest nose flute jazz performances ever recorded."),
            new("The Nostril Quartet - Breathtaking Sessions", "The Nostril Quartet", "Nose Flute Jazz", 2008, "Avant-garde nose flute improvisation at its peak."),
            new("Schnoz Davis - Kind of Blow", "Schnoz Davis", "Nose Flute Jazz", 1961, "Modal nose flute jazz tribute to the greats.")
        }
    };

    public VinylVaultSeeder(
        IContentService contentService,
        IWebHostEnvironment env,
        IRuntimeState runtimeState,
        ILogger<VinylVaultSeeder> logger)
    {
        _contentService = contentService;
        _env = env;
        _runtimeState = runtimeState;
        _logger = logger;
    }

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (_runtimeState.Level < RuntimeLevel.Run) return;
        if (!_env.IsDevelopment()) return;

        try
        {
            await Task.Run(() => SeedContent(), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "VINYL VAULT SEEDER: Unexpected error — safe to ignore");
        }
    }

    private void SeedContent()
    {
        var rootContent = _contentService.GetRootContent().ToList();

        var root = rootContent.FirstOrDefault();
        if (root == null)
        {
            _logger.LogWarning("VINYL VAULT SEEDER: No root content found — skipping");
            return;
        }

        // Check if Vinyl Vault already exists as a child of root (not at root level)
#pragma warning disable CS0618
        var children = _contentService.GetPagedChildren(root.Id, 0, 100, out _);
#pragma warning restore CS0618
        if (children.Any(c => c.ContentType.Alias == "vinylVaultHome"))
        {
            _logger.LogDebug("VINYL VAULT SEEDER: Content already exists — skipping");
            return;
        }

        _logger.LogInformation("VINYL VAULT SEEDER: Starting content seeding...");

        // Create Vinyl Vault Home node
        var vinylVaultHome = _contentService.Create("Vinyl Vault", root.Id, "vinylVaultHome");
        vinylVaultHome.SetValue("heroTitle", "Welcome to Vinyl Vault");
        vinylVaultHome.SetValue("heroSubtitle", "Your notification-powered vinyl record shop. Subscribe to genres and get instant alerts when new records drop.");
        
#pragma warning disable CS0618
        _contentService.Save(vinylVaultHome, null, null!);
        _contentService.Publish(vinylVaultHome, Array.Empty<string>(), Constants.Security.SuperUserId);
#pragma warning restore CS0618
        
        _logger.LogInformation("VINYL VAULT SEEDER: Created Vinyl Vault Home node (ID: {Id})", vinylVaultHome.Id);

        // Create genre landing pages and vinyl records
        foreach (var genreName in Genres)
        {
            CreateGenreAndVinyls(vinylVaultHome.Id, genreName);
        }

        _logger.LogInformation("VINYL VAULT SEEDER: Seeding completed successfully");
    }

    private void CreateGenreAndVinyls(int parentId, string genreName)
    {
        // Create genre landing page
        var genreLanding = _contentService.Create(genreName, parentId, "vinylGenreLanding");
        genreLanding.SetValue("genre", genreName);
        genreLanding.SetValue("description", GetGenreDescription(genreName));
        
#pragma warning disable CS0618
        _contentService.Save(genreLanding, null, null!);
        _contentService.Publish(genreLanding, Array.Empty<string>(), Constants.Security.SuperUserId);
#pragma warning restore CS0618
        
        _logger.LogInformation("VINYL VAULT SEEDER: Created genre landing page: {Genre} (ID: {Id})", genreName, genreLanding.Id);

        // Create vinyl records for this genre
        if (!VinylsByGenre.TryGetValue(genreName, out var vinyls))
            return;

        foreach (var vinyl in vinyls)
        {
            var vinylRecord = _contentService.Create(vinyl.Name, genreLanding.Id, "vinylRecord");
            vinylRecord.SetValue("title", vinyl.Name);
            vinylRecord.SetValue("artist", vinyl.Artist);
            vinylRecord.SetValue("genre", vinyl.Genre);
            vinylRecord.SetValue("releaseYear", vinyl.ReleaseYear);
            vinylRecord.SetValue("description", vinyl.Description);
            vinylRecord.SetValue("inStock", true);
            vinylRecord.SetValue("stockCount", 10);
            vinylRecord.SetValue("isLimitedEdition", false);
            vinylRecord.SetValue("notificationGenre", genreName); // Critical: matches genre for notification routing

#pragma warning disable CS0618
            _contentService.Save(vinylRecord, null, null!);
            _contentService.Publish(vinylRecord, Array.Empty<string>(), Constants.Security.SuperUserId);
#pragma warning restore CS0618
        }

        _logger.LogInformation("VINYL VAULT SEEDER: Created {Count} vinyl records for {Genre}", vinyls.Count, genreName);
    }

    private static string GetGenreDescription(string genre) => genre switch
    {
        "Jazz" => "From bebop to fusion, explore the finest jazz vinyl at Vinyl Vault.",
        "Rock" => "Classic rock albums that defined generations.",
        "Electronic" => "Cutting-edge electronic music on vinyl.",
        "Hip-Hop" => "The golden age of hip-hop, pressed on wax.",
        "Classical" => "Timeless classical masterpieces.",
        "Techno" => "Detroit techno and European minimal — the pulse of the underground.",
        "Nose Flute Jazz" => "The rarest and most avant-garde nose flute performances ever pressed to vinyl.",
        _ => "Discover rare and classic vinyl records."
    };

    private record VinylRecordSeed(string Name, string Artist, string Genre, int ReleaseYear, string Description);
}
