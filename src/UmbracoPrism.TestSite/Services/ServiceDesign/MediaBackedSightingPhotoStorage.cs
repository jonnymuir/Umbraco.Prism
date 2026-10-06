using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Extensions;
using Wayfinder.Models.ServiceDesign;
using Wayfinder.Umbraco.Services;

namespace UmbracoPrism.TestSite.Services.ServiceDesign;

/// <summary>
/// Stores the butterfly sighting photo in the Umbraco media library and hands back its media UDI
/// as the file's storage key, because that is the one form an Umbraco Automate "Run AI Agent"
/// attachment accepts (a media key, UDI or picker value). Every other file field is passed to the
/// disk-backed storage this wraps, so the juggling licence and bulk contributions uploads are
/// unaffected.
/// </summary>
/// <remarks>
/// Media is publicly servable by its path, so a photo (which also reveals where the practitioner
/// was) is only as private as that path is unguessable. See the design doc's privacy section.
/// Reads are restricted to media inside this storage's own folder, so a storage key can never be
/// used to open an unrelated media item.
/// </remarks>
public sealed class MediaBackedSightingPhotoStorage(
    IServiceRequestFileStorage inner,
    IMediaService mediaService,
    MediaFileManager mediaFileManager,
    MediaUrlGeneratorCollection mediaUrlGenerators,
    IShortStringHelper shortStringHelper,
    IContentTypeBaseServiceProvider contentTypeBaseServiceProvider,
    ILogger<MediaBackedSightingPhotoStorage> logger) : IServiceRequestFileStorage
{
    public const string PhotoFieldKey = "sightingPhoto";
    public const string FolderName = "Butterfly sightings";

    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private static readonly object FolderLock = new();

    public async Task<ServiceRequestFileReference> SaveAsync(
        string instanceId,
        string fieldKey,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(fieldKey, PhotoFieldKey, StringComparison.Ordinal))
        {
            return await inner.SaveAsync(instanceId, fieldKey, file, cancellationToken);
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        using var buffer = await ReadAsync(file, cancellationToken);
        EnsureAnImage(extension, buffer);

        var media = CreateMedia(extension, buffer);
        logger.LogInformation("Stored a sighting photo as media {MediaKey} for instance {InstanceId}.", media.Key, instanceId);
        return new ServiceRequestFileReference
        {
            StorageKey = Udi.Create(Constants.UdiEntityType.Media, media.Key).ToString(),
            OriginalFileName = Path.GetFileName(file.FileName),
            ContentType = ContentTypeFor(extension),
            SizeBytes = buffer.Length,
        };
    }

    public Task<Stream> OpenReadAsync(ServiceRequestFileReference reference, CancellationToken cancellationToken = default)
    {
        if (!UdiParser.TryParse(reference.StorageKey, out Udi? udi))
        {
            return inner.OpenReadAsync(reference, cancellationToken);
        }

        var media = FindSightingPhoto(udi)
            ?? throw new FileNotFoundException("No sighting photo is stored under that reference.");
        var path = StoredPath(media)
            ?? throw new FileNotFoundException("The sighting photo has no stored file.");

        return Task.FromResult(mediaFileManager.FileSystem.OpenFile(mediaFileManager.FileSystem.GetRelativePath(path)));
    }

    private static async Task<MemoryStream> ReadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var buffer = new MemoryStream();
        await using var source = file.OpenReadStream();
        await source.CopyToAsync(buffer, cancellationToken);
        return buffer;
    }

    private static void EnsureAnImage(string extension, MemoryStream buffer)
    {
        if (!AllowedExtensions.Contains(extension) || !LooksLikeAnImage(buffer.GetBuffer().AsSpan(0, (int)buffer.Length)))
        {
            throw new InvalidOperationException("The sighting photo must be a JPEG, PNG or WebP image.");
        }
    }

    private IMedia CreateMedia(string extension, MemoryStream buffer)
    {
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var media = mediaService.CreateMedia(
            $"Sighting {DateTime.UtcNow:yyyy-MM-dd HH-mm-ss}",
            EnsureFolder().Key,
            Constants.Conventions.MediaTypes.Image);
        buffer.Position = 0;
        media.SetValue(
            mediaFileManager, mediaUrlGenerators, shortStringHelper, contentTypeBaseServiceProvider,
            Constants.Conventions.Media.File, storedFileName, buffer);

        if (!mediaService.Save(media).Success)
        {
            throw new InvalidOperationException("The sighting photo could not be saved to the media library.");
        }

        return media;
    }

    /// <summary>The media item a UDI names, but only when it is a photo inside this storage's own folder.</summary>
    private IMedia? FindSightingPhoto(Udi udi)
    {
        if (udi is not GuidUdi { EntityType: Constants.UdiEntityType.Media } mediaUdi || FindFolder() is not { } folder)
        {
            return null;
        }

        var media = mediaService.GetById(mediaUdi.Guid);
        return media?.ParentId == folder.Id ? media : null;
    }

    private string? StoredPath(IMedia media)
    {
        var stored = media.GetValue<string>(Constants.Conventions.Media.File);
        var isJson = stored is not null && stored.TrimStart().StartsWith('{');
        var path = isJson ? JsonNode.Parse(stored!)?["src"]?.GetValue<string>() : stored;
        return string.IsNullOrEmpty(path) ? null : path;
    }

    private IMedia? FindFolder() =>
        mediaService.GetRootMedia().FirstOrDefault(m =>
            m.ContentType.Alias == Constants.Conventions.MediaTypes.Folder && m.Name == FolderName);

    private IMedia EnsureFolder()
    {
        lock (FolderLock)
        {
            if (FindFolder() is { } existing)
            {
                return existing;
            }

            var folder = mediaService.CreateMedia(FolderName, Constants.System.Root, Constants.Conventions.MediaTypes.Folder);
            mediaService.Save(folder);
            return folder;
        }
    }

    private static string ContentTypeFor(string extension) => extension switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg",
    };

    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static bool LooksLikeAnImage(ReadOnlySpan<byte> bytes) =>
        bytes.StartsWith(JpegSignature)
        || bytes.StartsWith(PngSignature)
        || (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8));
}
