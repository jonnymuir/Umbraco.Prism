using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using UmbracoPrism.TestSite.Services.ServiceDesign;
using Wayfinder.Models.ServiceDesign;
using Wayfinder.Umbraco.Services;

namespace UmbracoPrism.Core.Tests.FieldRecording;

/// <summary>
/// The sighting photo lives in the media library, which is publicly servable, so what may go in and
/// what may be read back is guarded. The paths that need Umbraco's file manager (a real save) are
/// proven against a live site; the guards here never reach it.
/// </summary>
public class MediaBackedSightingPhotoStorageTests
{
    private const int FolderId = 5;

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1];

    private readonly Mock<IServiceRequestFileStorage> inner = new();
    private readonly Mock<IMediaService> media = new();

    private MediaBackedSightingPhotoStorage Create() => new(
        inner.Object,
        media.Object,
        mediaFileManager: null!, // only reached by a real save, which these guards never get to
        new MediaUrlGeneratorCollection(() => []),
        Mock.Of<IShortStringHelper>(),
        Mock.Of<IContentTypeBaseServiceProvider>(),
        NullLogger<MediaBackedSightingPhotoStorage>.Instance);

    private static IFormFile File(string fileName, byte[] bytes) =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/octet-stream",
        };

    private static IMedia Folder()
    {
        var contentType = new Mock<ISimpleContentType>();
        contentType.SetupGet(c => c.Alias).Returns(Constants.Conventions.MediaTypes.Folder);
        var folder = new Mock<IMedia>();
        folder.SetupGet(f => f.Id).Returns(FolderId);
        folder.SetupGet(f => f.Name).Returns(MediaBackedSightingPhotoStorage.FolderName);
        folder.SetupGet(f => f.ContentType).Returns(contentType.Object);
        return folder.Object;
    }

    [Fact]
    public async Task AnyOtherFileField_GoesToTheDiskStorage_NotTheMediaLibrary()
    {
        var expected = new ServiceRequestFileReference { StorageKey = "instance/abc.pdf" };
        inner.Setup(i => i.SaveAsync("i1", "licence", It.IsAny<IFormFile>(), It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var saved = await Create().SaveAsync("i1", "licence", File("licence.pdf", Jpeg));

        saved.Should().BeSameAs(expected);
        media.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("photo.gif")]
    [InlineData("photo.svg")]
    [InlineData("photo.exe")]
    [InlineData("photo")]
    public async Task ThePhotoField_RejectsAnythingThatIsNotAJpegPngOrWebpByName(string fileName)
    {
        var act = () => Create().SaveAsync("i1", MediaBackedSightingPhotoStorage.PhotoFieldKey, File(fileName, Jpeg));

        await act.Should().ThrowAsync<InvalidOperationException>();
        media.VerifyNoOtherCalls();
        inner.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("photo.jpg")]
    [InlineData("photo.png")]
    [InlineData("photo.webp")]
    public async Task ThePhotoField_RejectsAnAllowedNameWhoseBytesAreNotAnImage(string fileName)
    {
        var script = System.Text.Encoding.UTF8.GetBytes("<script>alert(1)</script>");

        var act = () => Create().SaveAsync("i1", MediaBackedSightingPhotoStorage.PhotoFieldKey, File(fileName, script));

        await act.Should().ThrowAsync<InvalidOperationException>(
            "a renamed script or document must never be stored as a publicly servable image");
        media.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReadingADiskReference_GoesToTheDiskStorage()
    {
        var reference = new ServiceRequestFileReference { StorageKey = "instance/abc.pdf" };
        using var stream = new MemoryStream();
        inner.Setup(i => i.OpenReadAsync(reference, It.IsAny<CancellationToken>())).ReturnsAsync(stream);

        var opened = await Create().OpenReadAsync(reference);

        opened.Should().BeSameAs(stream);
    }

    [Fact]
    public async Task ReadingAMediaReference_OutsideTheSightingsFolder_IsRefused()
    {
        var other = new Mock<IMedia>();
        other.SetupGet(m => m.ParentId).Returns(99);
        var key = Guid.NewGuid();
        media.Setup(m => m.GetRootMedia()).Returns([Folder()]);
        media.Setup(m => m.GetById(key)).Returns(other.Object);

        var act = () => Create().OpenReadAsync(new ServiceRequestFileReference { StorageKey = $"umb://media/{key:N}" });

        await act.Should().ThrowAsync<FileNotFoundException>(
            "a storage key must never be a way to open an unrelated media item, such as the site's branding images");
    }

    [Fact]
    public async Task ReadingAMediaReference_WhenThereIsNoSightingsFolderYet_IsRefused()
    {
        media.Setup(m => m.GetRootMedia()).Returns([]);

        var act = () => Create().OpenReadAsync(new ServiceRequestFileReference { StorageKey = $"umb://media/{Guid.NewGuid():N}" });

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task ReadingAnUdiThatIsNotMedia_IsRefused()
    {
        media.Setup(m => m.GetRootMedia()).Returns([Folder()]);

        var act = () => Create().OpenReadAsync(new ServiceRequestFileReference { StorageKey = $"umb://document/{Guid.NewGuid():N}" });

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task ReadingAMediaReference_ThatDoesNotExist_IsRefused()
    {
        media.Setup(m => m.GetRootMedia()).Returns([Folder()]);
        media.Setup(m => m.GetById(It.IsAny<Guid>())).Returns((IMedia?)null);

        var act = () => Create().OpenReadAsync(new ServiceRequestFileReference { StorageKey = $"umb://media/{Guid.NewGuid():N}" });

        await act.Should().ThrowAsync<FileNotFoundException>();
    }
}
