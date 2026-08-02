using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Moq;
using Wayfinder.Umbraco.Configuration;
using Wayfinder.Umbraco.Models;
using Wayfinder.Umbraco.Services;
using Wayfinder.Models.ServiceDesign;

namespace UmbracoPrism.Core.Tests.Services.ServiceDesign;

public class StageNonceServiceTests
{
    // ------------------------------------------------------------------ Helpers

    private static StageNonceService BuildService(
        IDistributedCache? cache = null,
        WayfinderServiceDesignOptions? options = null)
    {
        cache ??= new Mock<IDistributedCache>().Object;
        options ??= new WayfinderServiceDesignOptions();
        return new StageNonceService(cache, Options.Create(options));
    }

    private static List<FieldRenderPayload> CreateSampleFields()
    {
        return new List<FieldRenderPayload>
        {
            new()
            {
                FieldKey = "name",
                Label = "Name",
                FieldType = "text",
                Required = true
            },
            new()
            {
                FieldKey = "email",
                Label = "Email",
                FieldType = "email",
                Required = true
            }
        };
    }

    // ------------------------------------------------------------------ CreateAsync

    [Fact]
    public async Task CreateAsync_ReturnsNonEmptyString()
    {
        var cacheMock = new Mock<IDistributedCache>();
        var service = BuildService(cache: cacheMock.Object);
        var fields = CreateSampleFields();

        var nonce = await service.CreateAsync(fields);

        nonce.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateAsync_NonceIs32HexChars()
    {
        var cacheMock = new Mock<IDistributedCache>();
        var service = BuildService(cache: cacheMock.Object);
        var fields = CreateSampleFields();

        var nonce = await service.CreateAsync(fields);

        nonce.Should().MatchRegex("^[0-9a-f]{32}$");
    }

    [Fact]
    public async Task CreateAsync_StoresCacheEntry()
    {
        var cacheMock = new Mock<IDistributedCache>();
        var service = BuildService(cache: cacheMock.Object);
        var fields = CreateSampleFields();

        await service.CreateAsync(fields);

        cacheMock.Verify(
            c => c.SetAsync(
                It.Is<string>(key => key.StartsWith("wayfinder:workflow:nonce:")),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_UsesTtlFromOptions()
    {
        var cacheMock = new Mock<IDistributedCache>();
        var options = new WayfinderServiceDesignOptions { NonceExpiry = TimeSpan.FromMinutes(30) };
        var service = BuildService(cache: cacheMock.Object, options: options);
        var fields = CreateSampleFields();

        await service.CreateAsync(fields);

        cacheMock.Verify(
            c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.Is<DistributedCacheEntryOptions>(opts =>
                    opts.AbsoluteExpirationRelativeToNow == TimeSpan.FromMinutes(30)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ------------------------------------------------------------------ ResolveAsync

    [Fact]
    public async Task ResolveAsync_ValidNonce_ReturnsOriginalFieldList()
    {
        var cacheMock = new Mock<IDistributedCache>();
        var service = BuildService(cache: cacheMock.Object);
        var fields = CreateSampleFields();

        var nonce = await service.CreateAsync(fields);

        cacheMock.Setup(c => c.GetAsync(
                It.Is<string>(k => k == $"wayfinder:workflow:nonce:{nonce}"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) =>
            {
                return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(fields, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                });
            });

        var resolved = await service.ResolveAsync(nonce);

        resolved.Should().NotBeNull();
        resolved.Should().HaveCount(2);
        resolved![0].FieldKey.Should().Be("name");
        resolved[1].FieldKey.Should().Be("email");
    }

    [Fact]
    public async Task ResolveAsync_UnknownNonce_ReturnsNull()
    {
        var cacheMock = new Mock<IDistributedCache>();
        cacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var service = BuildService(cache: cacheMock.Object);

        var resolved = await service.ResolveAsync("unknown-nonce");

        resolved.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_ExpiredNonce_ReturnsNull()
    {
        var cacheMock = new Mock<IDistributedCache>();
        cacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var service = BuildService(cache: cacheMock.Object);

        var resolved = await service.ResolveAsync("expired-nonce");

        resolved.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_CallsCacheWithCorrectKey()
    {
        var cacheMock = new Mock<IDistributedCache>();
        cacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);
        
        var service = BuildService(cache: cacheMock.Object);
        var nonce = "abc123def456abc123def456abc12345";

        await service.ResolveAsync(nonce);

        cacheMock.Verify(
            c => c.GetAsync(
                "wayfinder:workflow:nonce:abc123def456abc123def456abc12345",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ------------------------------------------------------------------ Round-trip

    [Fact]
    public async Task GivenCreatedNonce_WhenResolved_ThenReturnsIdenticalFields()
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
        var originalFields = new List<FieldRenderPayload>
        {
            new()
            {
                FieldKey = "username",
                Label = "Username",
                FieldType = "text",
                Required = true,
                MinLength = 3,
                MaxLength = 20
            },
            new()
            {
                FieldKey = "age",
                Label = "Age",
                FieldType = "number",
                Required = false,
                Min = 0,
                Max = 120
            }
        };

        var nonce = await service.CreateAsync(originalFields);
        var resolved = await service.ResolveAsync(nonce);

        resolved.Should().NotBeNull();
        resolved.Should().HaveCount(2);

        resolved![0].FieldKey.Should().Be("username");
        resolved[0].Label.Should().Be("Username");
        resolved[0].FieldType.Should().Be("text");
        resolved[0].Required.Should().BeTrue();
        resolved[0].MinLength.Should().Be(3);
        resolved[0].MaxLength.Should().Be(20);

        resolved[1].FieldKey.Should().Be("age");
        resolved[1].Label.Should().Be("Age");
        resolved[1].FieldType.Should().Be("number");
        resolved[1].Required.Should().BeFalse();
        resolved[1].Min.Should().Be(0);
        resolved[1].Max.Should().Be(120);
    }

    [Fact]
    public async Task GivenTwoNonces_WhenCreated_ThenDifferentValues()
    {
        var cacheMock = new Mock<IDistributedCache>();
        var service = BuildService(cache: cacheMock.Object);
        var fields = CreateSampleFields();

        var nonce1 = await service.CreateAsync(fields);
        var nonce2 = await service.CreateAsync(fields);

        nonce1.Should().NotBe(nonce2);
    }
}
