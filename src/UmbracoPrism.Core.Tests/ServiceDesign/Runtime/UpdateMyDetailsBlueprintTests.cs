using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UmbracoPrism.Core.Models;
using UmbracoPrism.TestSite.Services.ServiceDesign;
using Wayfinder.Engine.Abstractions;
using Wayfinder.Engine.Services;
using Wayfinder.Engine.Stores;
using Wayfinder.Models.ServiceDesign;
using Wayfinder.Services.Sanitization;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Runtime;

/// <summary>
/// "Update my details" is the reference for a front-stage journey that calls a business app AS the
/// signed-in member. These tests drive the real engine with the real
/// <see cref="MockBusinessAppProfileClient"/> over a stub HTTP handler, so what they pin down is
/// what an implementor relies on: whose token reaches the business app, what the member sees
/// because of the answer, and that the token goes nowhere else.
/// </summary>
public class UpdateMyDetailsBlueprintTests
{
    private const string DefinitionKey = "update-my-details";
    private const string TenantId = "PRISM-DEMO";
    private const string UserId = "demo@prism.local";
    private const string MemberToken = "member-access-token-do-not-leak";

    static UpdateMyDetailsBlueprintTests() => TestSupportSystems.EnsureRegistered();

    [Fact]
    public void Definition_PassesAuthoringValidation()
    {
        var authoringService = new ServiceBlueprintAuthoringService(new Mock<IServiceBlueprintSourceStore>().Object);

        var outcome = authoringService.Validate(LoadDefinition());

        outcome.Diagnostics.Should().BeEmpty(string.Join("; ", outcome.Diagnostics.Select(d => $"{d.Code} {d.Path}: {d.Message}")));
        outcome.IsValid.Should().BeTrue();
    }

    [Fact]
    public void RegisteredMember_IsLookedUpWithTheirOwnBearerToken_AndSeesTheirRecordPrefilled()
    {
        var business = new StubBusinessApp { ProfileResponse = Json(HttpStatusCode.OK, new
        {
            registered = true, tenant = "Prism Demo (Keycloak)", tenantCode = "PRISM-DEMO", name = "demo",
            email = "demo@prism.local", role = "Admin", phone = "01632 960 001", contactPreference = "phone",
        }) };
        var journey = new Journey(business);

        var form = journey.StartAndWaitForLookup();

        business.Requests.Should().ContainSingle();
        business.Requests[0].Method.Should().Be(HttpMethod.Get);
        business.Requests[0].Authorization.Should().Be($"Bearer {MemberToken}", "the business app must be asked as the member, not as the host");
        form.StateDisplayName.Should().Be("Your contact details");
        form.Text.Should().Contain("demo@prism.local").And.Contain("Prism Demo (Keycloak)")
            .And.Contain("01632 960 001", "the member's current phone number prefills the form");
    }

    [Fact]
    public void SignedInPersonWhoIsNotAMemberOfTheTenant_IsToldSo_NotShownAnEditForm()
    {
        var business = new StubBusinessApp { ProfileResponse = Json(HttpStatusCode.OK, new { registered = false, tenant = "Beta Services" }) };
        var journey = new Journey(business);

        var page = journey.StartAndWaitForLookup();

        page.StateDisplayName.Should().Be("We could not find your membership");
        page.Text.Should().Contain("Beta Services");
    }

    [Fact]
    public void WhenNoBearerTokenCanBeReleased_NothingIsSentToTheBusinessApp()
    {
        var business = new StubBusinessApp();
        var journey = new Journey(business, bearerToken: null);

        try { journey.StartAndWaitForLookup(); } catch (Exception) { /* the stage failing to start is acceptable; sending anonymously is not */ }

        business.Requests.Should().BeEmpty("a call made without the member's token would be answered for nobody, or for the wrong person");
    }

    [Fact]
    public void AcceptedChange_IsWrittenWithTheMembersToken_AndConfirmedWithTheBusinessAppsReference()
    {
        var business = RegisteredMember();
        business.SaveResponse = Json(HttpStatusCode.OK, new { reference = "UPD-ABC12345", tenantCode = "PRISM-DEMO" });
        var journey = new Journey(business);
        journey.StartAndWaitForLookup();

        var confirmation = journey.SubmitDetails(phone: "01632 960 002", preference: "email");

        var write = business.Requests.Single(r => r.Method == HttpMethod.Put);
        write.Authorization.Should().Be($"Bearer {MemberToken}");
        write.Body.Should().Contain("01632 960 002").And.Contain("email");
        confirmation.StateDisplayName.Should().Be("Details updated");
        confirmation.Text.Should().Contain("UPD-ABC12345");
    }

    [Fact]
    public void ChangeTheBusinessAppRefuses_ReturnsTheMemberToTheFormWithTheReason_AndNothingIsConfirmed()
    {
        var business = RegisteredMember();
        business.SaveResponse = Json(HttpStatusCode.UnprocessableEntity, new { error = "Enter a telephone number in the correct format, like 01632 960 001." });
        var journey = new Journey(business);
        journey.StartAndWaitForLookup();

        var page = journey.SubmitDetails(phone: "not a number", preference: "phone");

        page.StateDisplayName.Should().Be("We could not update your details");
        page.Text.Should().Contain("Enter a telephone number in the correct format");
    }

    [Fact]
    public void TheBearerToken_NeverAppearsInAnythingTheMemberIsShown()
    {
        var business = RegisteredMember();
        business.SaveResponse = Json(HttpStatusCode.OK, new { reference = "UPD-ABC12345", tenantCode = "PRISM-DEMO" });
        var journey = new Journey(business);
        var seen = new List<string> { journey.StartAndWaitForLookup().Text };

        seen.Add(journey.SubmitDetails(phone: "01632 960 002", preference: "email").Text);

        seen.Should().OnlyContain(text => !text.Contains(MemberToken),
            "the token is attached to the outgoing request only, never stored as a field the journey can render");
    }

    private static StubBusinessApp RegisteredMember() => new()
    {
        ProfileResponse = Json(HttpStatusCode.OK, new
        {
            registered = true, tenant = "Prism Demo (Keycloak)", tenantCode = "PRISM-DEMO", name = "demo",
            email = "demo@prism.local", role = "Admin", phone = "", contactPreference = "email",
        }),
    };

    private static HttpResponseMessage Json(HttpStatusCode status, object body) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };

    private static ServiceBlueprint LoadDefinition()
    {
        var path = Path.Combine(TestSupportSystems.FindTestSiteDirectory(), "service-blueprints", "update-my-details.json");
        return JsonSerializer.Deserialize<ServiceBlueprint>(File.ReadAllText(path), new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            AllowOutOfOrderMetadataProperties = true,
        }) ?? throw new InvalidOperationException("Deserialized to null.");
    }

    private sealed record RecordedRequest(HttpMethod Method, string? Authorization, string Body);

    private sealed class StubBusinessApp : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];
        public HttpResponseMessage? ProfileResponse { get; set; }
        public HttpResponseMessage? SaveResponse { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.Method,
                request.Headers.Authorization?.ToString(),
                request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            return request.Method == HttpMethod.Get
                ? ProfileResponse ?? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : SaveResponse ?? new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>What the member sees at one point in the journey.</summary>
    private sealed record Page(string? StateDisplayName, string Text);

    /// <summary>A signed-in member's browser: advances the real engine and polls it, as the stage page does.</summary>
    private sealed class Journey
    {
        private readonly ProcessManagerEngine _engine;
        private string _instanceId = "";
        private int _stateVersion;

        public Journey(StubBusinessApp business, string? bearerToken = MemberToken)
        {
            var prismContext = new Mock<IPrismContext>();
            prismContext.Setup(c => c.GetAuthorizationHeaderAsync(It.IsAny<bool>()))
                .ReturnsAsync(bearerToken is null ? null : new AuthenticationHeaderValue("Bearer", bearerToken));

            var memberRequest = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection().AddSingleton(prismContext.Object).BuildServiceProvider(),
            };

            var httpClients = new Mock<IHttpClientFactory>();
            httpClients.Setup(f => f.CreateClient(MockBusinessAppProfileClient.HttpClientName))
                .Returns(() => new HttpClient(business, disposeHandler: false) { BaseAddress = new Uri("https://business.test") });

            _engine = new ProcessManagerEngine(
                NullLogger.Instance,
                new SingleDefinitionServiceBlueprintStore(LoadDefinition()),
                new PassthroughContentSanitizer(),
                // Mirrors TestSiteComposer's generic fallback: a source: "service" field is whatever the
                // support system last wrote into the instance's field values.
                serviceInputsResolver: (instance, definition, _) =>
                    (definition.Calculations?.Fields ?? new Dictionary<string, Wayfinder.Models.ServiceDesign.Calculations.ServiceBlueprintCalculationField>())
                        .Where(field => string.Equals(field.Value.Source, "service", StringComparison.OrdinalIgnoreCase))
                        .ToDictionary(field => field.Key, field => instance.FieldValues.GetValueOrDefault(field.Key)),
                supportSystemClients: [new MockBusinessAppProfileClient(httpClients.Object, new HttpContextAccessor { HttpContext = memberRequest })]);
        }

        public Page StartAndWaitForLookup()
        {
            var start = _engine.GetCurrent(DefinitionKey, TenantId, UserId, PublicVisitorQueueProfile);
            Track(start);
            return Poll(_engine.Advance(start.InstanceId, TenantId, UserId, PublicVisitorQueueProfile, "continue", start.StateVersion, null));
        }

        public Page SubmitDetails(string phone, string preference)
        {
            var edited = _engine.Advance(_instanceId, TenantId, UserId, PublicVisitorQueueProfile, "continue", _stateVersion,
                new Dictionary<string, object?> { ["phone"] = phone, ["contactPreference"] = preference });
            Track(edited);
            return Poll(_engine.Advance(_instanceId, TenantId, UserId, PublicVisitorQueueProfile, "submit", _stateVersion, null));
        }

        // A deferred response is the join gateway's waiting screen: the browser polls GetCurrent until the
        // support system's answer has been collected and the journey moves on.
        private Page Poll(ServiceRequestResponseEnvelope response)
        {
            for (var attempt = 0; attempt < 5 && response.ResponseState == "defer"; attempt++)
            {
                Track(response);
                response = _engine.GetCurrent(DefinitionKey, TenantId, UserId, PublicVisitorQueueProfile, _instanceId);
            }

            Track(response);
            response.ResponseState.Should().BeOneOf(["render", "complete"], "the journey should have reached a page the member can see");
            response.Render.Should().NotBeNull();
            return new Page(response.Render!.StateDisplayName, JsonSerializer.Serialize(response.Render));
        }

        private void Track(ServiceRequestResponseEnvelope response)
        {
            _instanceId = response.InstanceId;
            _stateVersion = response.StateVersion;
        }

        private static readonly ActorProfile PublicVisitorQueueProfile = new()
        {
            VisibleQueues = ["public-visitor"],
            StartableQueues = ["public-visitor"],
            ActionableQueues = ["public-visitor"],
            RestrictToInstanceOwner = true,
        };
    }
}
