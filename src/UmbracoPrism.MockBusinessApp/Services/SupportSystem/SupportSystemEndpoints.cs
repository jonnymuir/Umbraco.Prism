using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace UmbracoPrism.MockBusinessApp.Services.SupportSystem;

/// <summary>
/// MockBusinessApp's sole remaining purpose: a real, separate downstream decisioning backend —
/// see docs/guides/support-systems.md in the core Wayfinder repo, and
/// <c>SafetyNetUnderwriting</c> (that repo's own reference implementation of the identical
/// pattern) for the shape this mirrors. A Wayfinder-hosted engine never calls this directly; a
/// host's own <c>ISupportSystemClient</c> implementation does, either polling
/// <see cref="MapGet"/>'s status endpoint or waiting on the webhook <see cref="Decide"/> fires.
/// </summary>
public static class SupportSystemEndpoints
{
    public static IEndpointRouteBuilder MapSupportSystem(this IEndpointRouteBuilder app)
    {
        app.MapPost("/submissions", Submit);
        app.MapGet("/submissions/{id}", GetStatus);
        app.MapGet("/queue", RenderQueue);
        app.MapPost("/queue/{id}/decide", Decide);

        return app;
    }

    private static async Task<IResult> Submit(HttpContext ctx, SupportSystemStore store)
    {
        JsonNode? body;
        try
        {
            body = await JsonNode.ParseAsync(ctx.Request.Body);
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "Request body must be valid JSON." });
        }

        var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        string? callbackUrl = null;
        if (body is JsonObject obj)
        {
            foreach (var (key, value) in obj)
            {
                if (string.Equals(key, "callbackUrl", StringComparison.OrdinalIgnoreCase))
                {
                    callbackUrl = value?.GetValue<string>();
                    continue;
                }
                fields[key] = value is null ? default : JsonSerializer.SerializeToElement(value);
            }
        }

        var submission = store.Add(new SupportSystemSubmission
        {
            Id = Guid.NewGuid().ToString("N"),
            Fields = fields,
            CallbackUrl = callbackUrl
        });

        return Results.Ok(new { submissionId = submission.Id, status = "pending" });
    }

    private static IResult GetStatus(string id, SupportSystemStore store)
    {
        var submission = store.Get(id);
        if (submission is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new
        {
            submissionId = submission.Id,
            status = submission.Decided ? "decided" : "pending",
            outcomeKey = submission.OutcomeKey
        });
    }

    private static IResult RenderQueue(SupportSystemStore store)
    {
        var pending = store.GetPending();

        string Esc(string s) => System.Net.WebUtility.HtmlEncode(s);

        var rows = pending.Count == 0
            ? "<tr><td colspan=\"4\">No pending submissions</td></tr>"
            : string.Join("\n", pending.Select(s => $"""
                <tr>
                  <td>{Esc(s.Id)}</td>
                  <td>{Esc(s.SubmittedAt.ToString("u"))}</td>
                  <td><pre>{Esc(JsonSerializer.Serialize(s.Fields))}</pre></td>
                  <td>
                    <form method="post" action="/queue/{Esc(s.Id)}/decide">
                      <button type="submit" name="outcomeKey" value="approved">Approve</button>
                      <button type="submit" name="outcomeKey" value="rejected">Reject</button>
                    </form>
                  </td>
                </tr>
                """));

        var html = $"""
            <!doctype html>
            <html>
            <head><title>Support system queue</title></head>
            <body>
              <h1>Support system queue</h1>
              <table border="1" cellpadding="4">
                <thead><tr><th>Id</th><th>Submitted</th><th>Fields</th><th>Decide</th></tr></thead>
                <tbody>{rows}</tbody>
              </table>
            </body>
            </html>
            """;

        return Results.Content(html, "text/html");
    }

    private static async Task<IResult> Decide(
        string id, HttpContext ctx, SupportSystemStore store, IHttpClientFactory httpClientFactory, ILogger<Program> logger)
    {
        var outcomeKey = ctx.Request.Form["outcomeKey"].ToString();
        if (string.IsNullOrWhiteSpace(outcomeKey))
        {
            return Results.BadRequest(new { error = "outcomeKey is required." });
        }

        var decided = store.Decide(id, outcomeKey);
        if (decided is null)
        {
            return Results.NotFound();
        }

        if (!string.IsNullOrWhiteSpace(decided.CallbackUrl))
        {
            try
            {
                var client = httpClientFactory.CreateClient();
                await client.PostAsJsonAsync(decided.CallbackUrl, new { outcomeKey, resultPayload = decided.Fields });
            }
            catch (Exception ex)
            {
                // A demo callback failing shouldn't hide the decision itself from the caseworker
                // who just made it — log and still redirect to the (now-shorter) queue.
                logger.LogWarning(ex, "Support system callback to {CallbackUrl} failed for submission {Id}", decided.CallbackUrl, id);
            }
        }

        return Results.Redirect("/queue");
    }
}
