using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Net.Http.Json;
using AwesomeAssertions;
using UmbracoPrism.MockBusinessApp.Services.SupportSystem;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Security regression checks for the contributions routes: they need a valid token, a submission can only
/// be read back by whoever made it, the submitter is read from the token and never the request, and an
/// upload larger than a contributions file is refused.
/// </summary>
public sealed class MockBusinessAppContributionsEndpointsTests : IAsyncDisposable
{
    private const string Csv = "memberRef,memberName,tier,fireEndorsement,under18,dob,monthlyContribution\nNJF-001,Alice,Recreational,N,N,,15.00\n";

    private readonly BusinessAppTestHost _host = new();

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task EveryContributionsRoute_RefusesACallerWithNoToken()
    {
        using var anonymous = _host.Anonymous();

        (await anonymous.PostAsync("/contributions/submissions", Upload(Csv))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync("/contributions/submissions/anything")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync("/contributions/submissions/anything/file")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ASubmitter_CanReadBackTheirOwnSubmissionAndItsFile()
    {
        using var pat = _host.As("pat@example.test", BusinessAppTestHost.AlphaTenant);

        var id = await SubmitAsync(pat);
        await Task.Delay(TimeSpan.FromSeconds(3.2)); // the demo holds results back for three seconds

        (await pat.GetAsync($"/contributions/submissions/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await pat.GetAsync($"/contributions/submissions/{id}/file")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task KnowingASubmissionId_GrantsNothing_ToAnotherMemberOfTheSameTenant()
    {
        using var pat = _host.As("pat@example.test", BusinessAppTestHost.AlphaTenant);
        using var sam = _host.As("sam@example.test", BusinessAppTestHost.AlphaTenant);
        var id = await SubmitAsync(pat);
        await Task.Delay(TimeSpan.FromSeconds(3.2));

        (await sam.GetAsync($"/contributions/submissions/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await sam.GetAsync($"/contributions/submissions/{id}/file")).StatusCode.Should().Be(HttpStatusCode.NotFound,
            "someone else's id must look exactly like an unknown id, so ids cannot be probed");
    }

    [Fact]
    public async Task KnowingASubmissionId_GrantsNothing_ToTheSamePersonUnderAnotherTenant()
    {
        using var patInAlpha = _host.As("pat@example.test", BusinessAppTestHost.AlphaTenant);
        using var patInBeta = _host.As("pat@example.test", BusinessAppTestHost.BetaTenant);
        var id = await SubmitAsync(patInAlpha);
        await Task.Delay(TimeSpan.FromSeconds(3.2));

        (await patInBeta.GetAsync($"/contributions/submissions/{id}/file")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ATokenForAnUnknownTenant_CannotSubmit()
    {
        using var stranger = _host.As("pat@example.test", "00000000-0000-0000-0000-0000000000ff");

        (await stranger.PostAsync("/contributions/submissions", Upload(Csv))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AnUploadLargerThanAContributionsFile_IsRefused()
    {
        using var pat = _host.As("pat@example.test", BusinessAppTestHost.AlphaTenant);
        var oversized = new string('x', ContributionsEndpoints.MaxUploadBytes + 1024);

        using var response = await pat.PostAsync("/contributions/submissions", Upload(oversized));

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge, "an oversized upload must be refused and never stored");
    }

    [Fact]
    public void TheStoreIsBounded_SoAnAuthenticatedCallerCannotGrowItWithoutLimit()
    {
        var store = new ContributionsStore();
        for (var i = 0; i < ContributionsStore.MaxSubmissions + 50; i++)
        {
            store.Add(new ContributionsSubmission
            {
                Id = $"s{i}", OwnerKey = "owner", SubmittedAt = DateTimeOffset.UtcNow.AddSeconds(i), ResultCsvBytes = [],
            });
        }

        var kept = Enumerable.Range(0, ContributionsStore.MaxSubmissions + 50).Count(i => store.Get($"s{i}", "owner") is not null);
        kept.Should().Be(ContributionsStore.MaxSubmissions);
        store.Get($"s{ContributionsStore.MaxSubmissions + 49}", "owner").Should().NotBeNull("the newest is kept");
        store.Get("s0", "owner").Should().BeNull("the oldest is evicted");
    }

    private static async Task<string> SubmitAsync(HttpClient client)
    {
        using var response = await client.PostAsync("/contributions/submissions", Upload(Csv));
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        return (await response.Content.ReadFromJsonAsync<JsonObject>())!["submissionId"]!.GetValue<string>();
    }

    private static MultipartFormDataContent Upload(string csv)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "contributions.csv");
        return form;
    }
}
