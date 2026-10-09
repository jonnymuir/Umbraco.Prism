using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using UmbracoPrism.Core.Models;
using UmbracoPrism.TestSite.Services.ServiceDesign;
using Wayfinder.Engine.Abstractions;
using Wayfinder.Models.ServiceDesign;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// The rules for releasing a member's token to a business app, which every support-system client that
/// acts as the member relies on. Each is a way the token could otherwise end up somewhere it should not.
/// </summary>
public class MemberBearerProviderTests
{
    private const string Token = "member-access-token-do-not-leak";

    [Fact]
    public async Task ASignedInMember_GetsTheirOwnTokenForAnHttpsBusinessApp()
    {
        var provider = ProviderFor(new AuthenticationHeaderValue("Bearer", Token));

        var header = await provider.GetForAsync(BusinessApp("https://business.test"));

        header.ToString().Should().Be($"Bearer {Token}");
    }

    [Theory]
    [InlineData("http://business.test")]
    [InlineData("ftp://business.test")]
    public async Task TheTokenIsNeverReleasedForADestinationThatIsNotHttps(string address)
    {
        var provider = ProviderFor(new AuthenticationHeaderValue("Bearer", Token));

        var act = () => provider.GetForAsync(BusinessApp(address));

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().NotContain(Token);
    }

    [Fact]
    public async Task WithNoBaseAddressConfigured_TheTokenIsNotReleased()
    {
        var provider = ProviderFor(new AuthenticationHeaderValue("Bearer", Token));

        var act = () => provider.GetForAsync(new HttpClient());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task OutsideAnyRequest_NoTokenCanBeReleased()
    {
        var provider = new MemberBearerProvider(new HttpContextAccessor { HttpContext = null });

        var act = () => provider.GetForAsync(BusinessApp("https://business.test"));

        await act.Should().ThrowAsync<InvalidOperationException>("a call with no member to act for must not be made anonymously");
    }

    [Fact]
    public async Task WhenTheSessionCannotProduceAToken_NothingIsReleased_AndTheReasonDoesNotContainOne()
    {
        var provider = ProviderFor(header: null, failureReason: "tenant-mismatch");

        var act = () => provider.GetForAsync(BusinessApp("https://business.test"));

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("tenant-mismatch");
    }

    [Fact]
    public async Task TheContributionsClient_CallsAsTheMember_OnSubmitAndOnEveryPoll()
    {
        var business = new RecordingHandler();
        var fileStorage = new Mock<IServiceRequestFileStorage>();
        fileStorage.Setup(f => f.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("memberRef\nNJF-001")));
        var httpClients = new Mock<IHttpClientFactory>();
        httpClients.Setup(f => f.CreateClient(MockBusinessAppContributionsClient.HttpClientName))
            .Returns(() => new HttpClient(business, disposeHandler: false) { BaseAddress = new Uri("https://business.test") });
        var client = new MockBusinessAppContributionsClient(
            httpClients.Object, ProviderFor(new AuthenticationHeaderValue("Bearer", Token)), fileStorage.Object);

        var receipt = await client.InvokeAsync(
            MockBusinessAppContributions.ValidateContributionsFileCapability,
            new Dictionary<string, SupportSystemInputValue>
            {
                ["file"] = SupportSystemInputValue.Resolve(new ServiceRequestFileReference
                {
                    StorageKey = "k", OriginalFileName = "c.csv", ContentType = "text/csv", SizeBytes = 10,
                }),
            },
            new SupportSystemInvocationContext { InstanceId = "i-1", InvocationId = "inv-1", WebhookExpected = false });
        await client.CheckStatusAsync(MockBusinessAppContributions.ValidateContributionsFileCapability, receipt);

        business.Authorizations.Should().NotBeEmpty().And.OnlyContain(a => a == $"Bearer {Token}",
            "no call to the business app may be anonymous, or made as anyone but the member");
        business.Authorizations.Count.Should().BeGreaterThanOrEqualTo(2, "the submit and at least one poll were both made");
    }

    private static MemberBearerProvider ProviderFor(AuthenticationHeaderValue? header, string? failureReason = null)
    {
        var prismContext = new Mock<IPrismContext>();
        prismContext.Setup(c => c.GetAuthorizationHeaderAsync(It.IsAny<bool>())).ReturnsAsync(header);
        prismContext.SetupGet(c => c.LastAuthorizationFailureReason).Returns(failureReason);
        var request = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddSingleton(prismContext.Object).BuildServiceProvider(),
        };
        return new MemberBearerProvider(new HttpContextAccessor { HttpContext = request });
    }

    private static HttpClient BusinessApp(string address) => new() { BaseAddress = new Uri(address) };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string?> Authorizations { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorizations.Add(request.Headers.Authorization?.ToString());
            var body = request.RequestUri!.AbsolutePath.EndsWith("/file", StringComparison.Ordinal)
                ? "memberRef,status\nNJF-001,ok"
                : request.Method == HttpMethod.Post
                    ? """{"submissionId":"sub-1","status":"pending"}"""
                    : """{"id":"sub-1","status":"processed"}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
