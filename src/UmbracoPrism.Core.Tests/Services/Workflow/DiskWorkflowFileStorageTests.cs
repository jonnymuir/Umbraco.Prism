using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Moq;
using UmbracoPrism.Core.Services.Workflow;

namespace UmbracoPrism.Core.Tests.Services.Workflow;

public class DiskWorkflowFileStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"prism-file-storage-tests-{Guid.NewGuid():N}");
    private readonly DiskWorkflowFileStorage _storage;

    public DiskWorkflowFileStorageTests()
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.ContentRootPath).Returns(_root);
        _storage = new DiskWorkflowFileStorage(environment.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static IFormFile CreateFile(string fileName, string content, string contentType = "application/pdf")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    [Fact]
    public async Task SaveAsync_ThenOpenReadAsync_RoundTripsTheOriginalContent()
    {
        var file = CreateFile("Current Licence.pdf", "some pdf bytes");

        var reference = await _storage.SaveAsync("instance-1", "current-licence", file);
        await using var stream = await _storage.OpenReadAsync(reference);
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        content.Should().Be("some pdf bytes");
        reference.OriginalFileName.Should().Be("Current Licence.pdf");
        reference.ContentType.Should().Be("application/pdf");
        reference.SizeBytes.Should().Be(file.Length);
    }

    [Fact]
    public async Task SaveAsync_NeverUsesTheOriginalFileNameAsTheStorageKey()
    {
        var file = CreateFile("../../etc/passwd", "malicious");

        var reference = await _storage.SaveAsync("instance-1", "field", file);

        reference.StorageKey.Should().NotContain("passwd");
        reference.StorageKey.Should().NotContain("..");
    }

    [Fact]
    public async Task SaveAsync_WithPathTraversalInInstanceId_DoesNotEscapeTheStorageRoot()
    {
        var file = CreateFile("evidence.pdf", "content");

        var reference = await _storage.SaveAsync("../../outside", "field", file);

        var fullPath = Path.GetFullPath(Path.Combine(_root, reference.StorageKey));
        var normalizedRoot = Path.GetFullPath(_root) + Path.DirectorySeparatorChar;
        fullPath.Should().StartWith(normalizedRoot, "a traversal-shaped instanceId must never escape the storage root");
    }

    [Fact]
    public async Task DifferentInstances_DoNotCollideOnDisk()
    {
        var fileA = CreateFile("evidence.pdf", "content A");
        var fileB = CreateFile("evidence.pdf", "content B");

        var referenceA = await _storage.SaveAsync("instance-a", "field", fileA);
        var referenceB = await _storage.SaveAsync("instance-b", "field", fileB);

        referenceA.StorageKey.Should().NotBe(referenceB.StorageKey);

        await using var streamA = await _storage.OpenReadAsync(referenceA);
        await using var streamB = await _storage.OpenReadAsync(referenceB);
        (await new StreamReader(streamA).ReadToEndAsync()).Should().Be("content A");
        (await new StreamReader(streamB).ReadToEndAsync()).Should().Be("content B");
    }

    [Fact]
    public async Task OpenReadAsync_WithTamperedStorageKeyEscapingTheRoot_Throws()
    {
        var file = CreateFile("evidence.pdf", "content");
        var reference = await _storage.SaveAsync("instance-1", "field", file);

        var tampered = reference with { StorageKey = "../../../../etc/passwd" };

        var act = async () => await _storage.OpenReadAsync(tampered);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
