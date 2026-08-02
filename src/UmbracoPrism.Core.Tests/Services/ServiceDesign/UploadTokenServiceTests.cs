using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Moq;
using Wayfinder.Umbraco.Configuration;
using Wayfinder.Umbraco.Services;
using Wayfinder.Models.ServiceDesign;

namespace UmbracoPrism.Core.Tests.Services.Workflow;

public class UploadTokenServiceTests
{
    private static UploadTokenService BuildService(
        IDistributedCache? cache = null,
        WayfinderServiceDesignOptions? options = null)
    {
        cache ??= new Mock<IDistributedCache>().Object;
        options ??= new WayfinderServiceDesignOptions();
        return new UploadTokenService(cache, Options.Create(options));
    }

    private static ServiceRequestFileReference SampleReference() => new()
    {
        StorageKey = "abc123.pdf",
        OriginalFileName = "evidence.pdf",
        ContentType = "application/pdf",
        SizeBytes = 1024
    };

    // ------------------------------------------------------------------ CreateAsync

    [Fact]
    public async Task CreateAsync_ReturnsNonEmptyString()
    {
        var service = BuildService();

        var token = await service.CreateAsync("instance-1", "current-licence-upload", SampleReference());

        token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateAsync_TokenIs32HexChars()
    {
        var service = BuildService();

        var token = await service.CreateAsync("instance-1", "current-licence-upload", SampleReference());

        token.Should().MatchRegex("^[0-9a-f]{32}$");
    }

    [Fact]
    public async Task CreateAsync_StoresCacheEntryUnderUploadTokenPrefix()
    {
        var cacheMock = new Mock<IDistributedCache>();
        var service = BuildService(cache: cacheMock.Object);

        await service.CreateAsync("instance-1", "current-licence-upload", SampleReference());

        cacheMock.Verify(
            c => c.SetAsync(
                It.Is<string>(key => key.StartsWith("wayfinder:workflow:upload-token:")),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_UsesTtlFromOptions()
    {
        var cacheMock = new Mock<IDistributedCache>();
        var options = new WayfinderServiceDesignOptions { NonceExpiry = TimeSpan.FromMinutes(45) };
        var service = BuildService(cache: cacheMock.Object, options: options);

        await service.CreateAsync("instance-1", "current-licence-upload", SampleReference());

        cacheMock.Verify(
            c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.Is<DistributedCacheEntryOptions>(opts =>
                    opts.AbsoluteExpirationRelativeToNow == TimeSpan.FromMinutes(45)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GivenTwoTokens_WhenCreated_ThenDifferentValues()
    {
        var service = BuildService();

        var token1 = await service.CreateAsync("instance-1", "field-a", SampleReference());
        var token2 = await service.CreateAsync("instance-1", "field-a", SampleReference());

        token1.Should().NotBe(token2);
    }

    // ------------------------------------------------------------------ ResolveAsync

    [Fact]
    public async Task ResolveAsync_UnknownToken_ReturnsNull()
    {
        var cacheMock = new Mock<IDistributedCache>();
        cacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var service = BuildService(cache: cacheMock.Object);

        var resolved = await service.ResolveAsync("unknown-token");

        resolved.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_CallsCacheWithCorrectKey()
    {
        var cacheMock = new Mock<IDistributedCache>();
        cacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var service = BuildService(cache: cacheMock.Object);

        await service.ResolveAsync("abc123def456abc123def456abc12345");

        cacheMock.Verify(
            c => c.GetAsync(
                "wayfinder:workflow:upload-token:abc123def456abc123def456abc12345",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ------------------------------------------------------------------ Round-trip

    [Fact]
    public async Task GivenCreatedToken_WhenResolved_ThenBindingMatchesInstanceFieldAndReference()
    {
        var storedData = (byte[]?)null;
        var cacheMock = new Mock<IDistributedCache>();

        cacheMock.Setup(c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                (_, data, _, _) => storedData = data)
            .Returns(Task.CompletedTask);

        cacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => storedData);

        var service = BuildService(cache: cacheMock.Object);
        var reference = SampleReference();

        var token = await service.CreateAsync("instance-42", "proof-of-identity-upload", reference);
        var resolved = await service.ResolveAsync(token);

        resolved.Should().NotBeNull();
        resolved!.InstanceId.Should().Be("instance-42");
        resolved.FieldKey.Should().Be("proof-of-identity-upload");
        resolved.Reference.StorageKey.Should().Be(reference.StorageKey);
        resolved.Reference.OriginalFileName.Should().Be(reference.OriginalFileName);
        resolved.Reference.ContentType.Should().Be(reference.ContentType);
        resolved.Reference.SizeBytes.Should().Be(reference.SizeBytes);
    }
}
