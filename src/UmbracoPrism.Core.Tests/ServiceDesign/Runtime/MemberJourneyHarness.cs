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
/// The shared rig for journeys that call the business app as the signed-in person: the real engine
/// and the real <see cref="MockBusinessAppProfileClient"/> over a stub HTTP handler, so a test sees
/// exactly what reaches the business app and what the person is shown because of the answer.
/// </summary>
internal static class MemberJourneyHarness
{
    public const string TenantId = "PRISM-DEMO";
    public const string UserId = "demo@prism.local";
    public const string MemberToken = "member-access-token-do-not-leak";

    public static HttpResponseMessage Reply(HttpStatusCode status, object body) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };

    public static ServiceBlueprint LoadDefinition(string definitionKey)
    {
        var path = Path.Combine(TestSupportSystems.FindTestSiteDirectory(), "service-blueprints", $"{definitionKey}.json");
        return JsonSerializer.Deserialize<ServiceBlueprint>(File.ReadAllText(path), new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            AllowOutOfOrderMetadataProperties = true,
        }) ?? throw new InvalidOperationException("Deserialized to null.");
    }

    public sealed record RecordedRequest(HttpMethod Method, string? Authorization, string Body);

    /// <summary>The business app: a GET is the profile lookup, a PUT the profile save, a POST the registration.</summary>
    public sealed class StubBusinessApp : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];
        public HttpResponseMessage? ProfileResponse { get; set; }
        public HttpResponseMessage? SaveResponse { get; set; }
        public HttpResponseMessage? RegisterResponse { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.Method,
                request.Headers.Authorization?.ToString(),
                request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            var answer = request.Method.Method switch
            {
                "GET" => ProfileResponse,
                "PUT" => SaveResponse,
                "POST" => RegisterResponse,
                _ => null,
            };
            return answer ?? new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>What the person sees at one point in the journey.</summary>
    public sealed record Page(string? StateDisplayName, string Text);

    /// <summary>A signed-in person's browser: advances the real engine and polls it, as the stage page does.</summary>
    public sealed class Journey
    {
        private readonly string _definitionKey;
        private readonly ProcessManagerEngine _engine;
        private string _instanceId = "";
        private int _stateVersion;

        public Journey(string definitionKey, StubBusinessApp business, string? bearerToken = MemberToken)
        {
            _definitionKey = definitionKey;
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
                new SingleDefinitionServiceBlueprintStore(LoadDefinition(definitionKey)),
                new PassthroughContentSanitizer(),
                // Mirrors TestSiteComposer's generic fallback: a source: "service" field is whatever the
                // support system last wrote into the instance's field values.
                serviceInputsResolver: (instance, definition, _) =>
                    (definition.Calculations?.Fields ?? new Dictionary<string, Wayfinder.Models.ServiceDesign.Calculations.ServiceBlueprintCalculationField>())
                        .Where(field => string.Equals(field.Value.Source, "service", StringComparison.OrdinalIgnoreCase))
                        .ToDictionary(field => field.Key, field => instance.FieldValues.GetValueOrDefault(field.Key)),
                supportSystemClients: [new MockBusinessAppProfileClient(httpClients.Object, new MemberBearerProvider(new HttpContextAccessor { HttpContext = memberRequest }))]);
        }

        /// <summary>Opens the journey and returns its first page.</summary>
        public Page Start()
        {
            var start = _engine.GetCurrent(_definitionKey, TenantId, UserId, PublicVisitorQueueProfile);
            Track(start);
            return ToPage(start);
        }

        /// <summary>Takes <paramref name="trigger"/> from the current page, optionally with answers, and returns where the person lands.</summary>
        public Page Advance(string trigger, Dictionary<string, object?>? answers = null) =>
            Poll(_engine.Advance(_instanceId, TenantId, UserId, PublicVisitorQueueProfile, trigger, _stateVersion, answers));

        // A deferred response is the join gateway's waiting screen: the browser polls GetCurrent until the
        // support system's answer has been collected and the journey moves on.
        private Page Poll(ServiceRequestResponseEnvelope response)
        {
            for (var attempt = 0; attempt < 5 && response.ResponseState == "defer"; attempt++)
            {
                Track(response);
                response = _engine.GetCurrent(_definitionKey, TenantId, UserId, PublicVisitorQueueProfile, _instanceId);
            }

            Track(response);
            response.ResponseState.Should().BeOneOf(["render", "complete"], "the journey should have reached a page the person can see");
            return ToPage(response);
        }

        private static Page ToPage(ServiceRequestResponseEnvelope response)
        {
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
