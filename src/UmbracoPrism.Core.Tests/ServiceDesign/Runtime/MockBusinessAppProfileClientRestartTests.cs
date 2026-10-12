using System.Net;
using System.Net.Http.Headers;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using UmbracoPrism.Core.Models;
using UmbracoPrism.TestSite.Services.ServiceDesign;
using Wayfinder.Engine.Abstractions;
using static UmbracoPrism.Core.Tests.ServiceDesign.Runtime.MemberJourneyHarness;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Runtime;

/// <summary>
/// The business app's answer is held by the web app between sending a call and the journey collecting
/// it. If the web app restarts in between (a deploy, a reboot), that answer is gone, and a journey that
/// waits for it would wait for ever even though the business app has already acted. These tests stand
/// in for the restart with a second client that never saw the call.
/// </summary>
public class MockBusinessAppProfileClientRestartTests
{
    static MockBusinessAppProfileClientRestartTests() => TestSupportSystems.EnsureRegistered();

    private static MockBusinessAppProfileClient NewClient(StubBusinessApp business)
    {
        var prismContext = new Mock<IPrismContext>();
        prismContext.Setup(c => c.GetAuthorizationHeaderAsync(It.IsAny<bool>()))
            .ReturnsAsync(new AuthenticationHeaderValue("Bearer", MemberToken));
        var request = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddSingleton(prismContext.Object).BuildServiceProvider(),
        };
        var httpClients = new Mock<IHttpClientFactory>();
        httpClients.Setup(f => f.CreateClient(MockBusinessAppProfileClient.HttpClientName))
            .Returns(() => new HttpClient(business, disposeHandler: false) { BaseAddress = new Uri("https://business.test") });
        return new MockBusinessAppProfileClient(httpClients.Object, new MemberBearerProvider(new HttpContextAccessor { HttpContext = request }));
    }

    private static SupportSystemInvocationContext Context() =>
        new() { InstanceId = "instance-1", InvocationId = "invocation-1", WebhookExpected = false };

    private static Dictionary<string, SupportSystemInputValue> Details() => new()
    {
        ["memberName"] = new() { RawValue = "Robin Reed" },
        ["phone"] = new() { RawValue = "" },
        ["contactPreference"] = new() { RawValue = "email" },
    };

    private static async Task<SupportSystemInvocationReceipt> SendRegistration(StubBusinessApp business)
    {
        var receipt = await NewClient(business).InvokeAsync(MockBusinessAppProfile.RegisterMemberCapability, Details(), Context());
        return receipt;
    }

    [Fact]
    public async Task ARegistrationTheBusinessAppAccepted_IsStillFound_WhenTheWebAppRestartedBeforeTheAnswerWasCollected()
    {
        var business = new StubBusinessApp
        {
            RegisterResponse = Reply(HttpStatusCode.Created, new { registered = true, created = true, tenant = "Prism Reference", tenantCode = "PRISM-REFERENCE", backOfficeId = "MBR-1A2B3C4D", role = "Member" }),
            ProfileResponse = Reply(HttpStatusCode.OK, new { registered = true, tenant = "Prism Reference", tenantCode = "PRISM-REFERENCE", backOfficeId = "MBR-1A2B3C4D", name = "Robin Reed", email = "robin@example.test", role = "Member", phone = "", contactPreference = "email" }),
        };
        var receipt = await SendRegistration(business);

        var afterRestart = NewClient(business);
        var outcome = await afterRestart.CheckStatusAsync(MockBusinessAppProfile.RegisterMemberCapability, receipt);

        outcome.Should().NotBeNull("the journey must not wait for ever for an answer that only existed in the old process");
        outcome!.OutcomeKey.Should().Be(MockBusinessAppProfile.RegisteredMemberOutcome);
        outcome.ResultPayload!["memberId"]!.GetValue<string>().Should().Be("MBR-1A2B3C4D");
    }

    [Fact]
    public async Task ARegistrationThatNeverReachedTheBusinessApp_SendsThePersonBackToTheFormInsteadOfWaitingForEver()
    {
        var business = new StubBusinessApp
        {
            RegisterResponse = Reply(HttpStatusCode.Created, new { registered = true, created = true, tenant = "Prism Reference", tenantCode = "PRISM-REFERENCE", backOfficeId = "MBR-1A2B3C4D", role = "Member" }),
            ProfileResponse = Reply(HttpStatusCode.OK, new { registered = false, tenant = "Prism Reference" }),
        };
        var receipt = await SendRegistration(business);

        var outcome = await NewClient(business).CheckStatusAsync(MockBusinessAppProfile.RegisterMemberCapability, receipt);

        outcome.Should().NotBeNull();
        outcome!.OutcomeKey.Should().Be(MockBusinessAppProfile.RejectedOutcome);
        outcome.ResultPayload!["registrationNote"]!.GetValue<string>().Should().Contain("try again");
    }

    [Fact]
    public async Task WhenTheBusinessAppCannotBeAskedAfterARestart_TheJourneyKeepsWaiting_NotGuessing()
    {
        var business = new StubBusinessApp
        {
            RegisterResponse = Reply(HttpStatusCode.Created, new { registered = true, created = true, tenant = "Prism Reference", tenantCode = "PRISM-REFERENCE", backOfficeId = "MBR-1A2B3C4D", role = "Member" }),
            ProfileResponse = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
        };
        var receipt = await SendRegistration(business);

        var outcome = await NewClient(business).CheckStatusAsync(MockBusinessAppProfile.RegisterMemberCapability, receipt);

        outcome.Should().BeNull("an unanswered question is not an answer; the next poll asks again");
    }

    [Fact]
    public async Task AnAnswerStillHeldInMemory_IsReturnedOnce_WithoutAskingTheBusinessAppAgain()
    {
        var business = new StubBusinessApp
        {
            RegisterResponse = Reply(HttpStatusCode.Created, new { registered = true, created = true, tenant = "Prism Reference", tenantCode = "PRISM-REFERENCE", backOfficeId = "MBR-1A2B3C4D", role = "Member" }),
        };
        var client = NewClient(business);
        var receipt = await client.InvokeAsync(MockBusinessAppProfile.RegisterMemberCapability, Details(), Context());

        var outcome = await client.CheckStatusAsync(MockBusinessAppProfile.RegisterMemberCapability, receipt);

        outcome!.OutcomeKey.Should().Be(MockBusinessAppProfile.RegisteredMemberOutcome);
        business.Requests.Should().ContainSingle("only the registration itself was sent");
    }
}
