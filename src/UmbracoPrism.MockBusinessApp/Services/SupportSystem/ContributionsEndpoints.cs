namespace UmbracoPrism.MockBusinessApp.Services.SupportSystem;

/// <summary>
/// Contributions-file validation — a genuinely different interaction shape from
/// <see cref="SupportSystemEndpoints"/>'s human-decided submissions above: no staff member
/// decides anything here, <see cref="ContributionsValidation"/> applies deterministic rules
/// automatically the moment the file arrives. Mirrors the core Wayfinder repo's own
/// <c>SafetyNetUnderwriting/Program.cs</c> contributions endpoints — same shape, this app's own
/// store/validation.
/// </summary>
public static class ContributionsEndpoints
{
    public static IEndpointRouteBuilder MapContributions(this IEndpointRouteBuilder app)
    {
        app.MapPost("/contributions/submissions", PostSubmission);
        app.MapGet("/contributions/submissions/{id}", GetStatus);
        app.MapGet("/contributions/submissions/{id}/file", GetFile);

        return app;
    }

    private static async Task<IResult> PostSubmission(HttpRequest request, ContributionsStore store)
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest("Expected multipart/form-data.");
        }

        var form = await request.ReadFormAsync();
        var file = form.Files["file"];
        if (file is null)
        {
            return Results.BadRequest("Expected a 'file' part.");
        }

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        var resultCsv = ContributionsValidation.Validate(stream.ToArray());

        var id = Guid.NewGuid().ToString("N");
        store.Add(new ContributionsSubmission
        {
            Id = id,
            SubmittedAt = DateTimeOffset.UtcNow,
            // A short artificial delay so the demo genuinely shows a "please wait while we
            // process your file" screen instead of resolving on the very first poll — real batch
            // processing isn't instant either. Purely a demo touch: the actual validation already
            // ran above, this just holds back when it's revealed as done.
            ReadyAt = DateTimeOffset.UtcNow.AddSeconds(3),
            ResultCsvBytes = resultCsv,
        });

        return Results.Accepted($"/contributions/submissions/{id}", new { submissionId = id, status = "pending" });
    }

    private static IResult GetStatus(string id, ContributionsStore store)
    {
        var submission = store.Get(id);
        if (submission is null)
        {
            return Results.NotFound();
        }

        var status = DateTimeOffset.UtcNow >= submission.ReadyAt ? "processed" : "pending";
        return Results.Ok(new { id = submission.Id, status });
    }

    private static IResult GetFile(string id, ContributionsStore store)
    {
        var submission = store.Get(id);
        if (submission is null || DateTimeOffset.UtcNow < submission.ReadyAt)
        {
            return Results.NotFound();
        }

        return Results.File(submission.ResultCsvBytes, "text/csv", "contributions-response.csv", enableRangeProcessing: false);
    }
}
