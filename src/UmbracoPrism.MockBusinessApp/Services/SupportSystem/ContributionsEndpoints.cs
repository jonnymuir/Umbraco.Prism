using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace UmbracoPrism.MockBusinessApp.Services.SupportSystem;

/// <summary>
/// Contributions-file validation: no staff member decides anything,
/// <see cref="ContributionsValidation"/> applies deterministic rules automatically the moment the
/// file arrives. Mirrors the core Wayfinder repo's own <c>SafetyNetUnderwriting/Program.cs</c>
/// contributions endpoints, with this app's own store and validation.
/// <para/>
/// Every route needs a valid bearer token and acts only on the caller's own submissions: the
/// submitter is read from the token, never the request, and a submission can only be read back by
/// whoever made it. Uploads are size-limited.
/// </summary>
public static class ContributionsEndpoints
{
    /// <summary>
    /// A contributions CSV is small. The limit is enforced here, in the handler, because
    /// <c>[RequestSizeLimit]</c> is an MVC filter and does not apply to minimal-API routes; the host's
    /// Kestrel body limit is a second, coarser line of defence.
    /// </summary>
    public const int MaxUploadBytes = 1_048_576;

    // Room for the multipart boundaries and headers around a file of exactly MaxUploadBytes.
    private const int MultipartOverheadBytes = 4_096;

    public static IEndpointRouteBuilder MapContributions(this IEndpointRouteBuilder app)
    {
        app.MapPost("/contributions/submissions", PostSubmission).RequireAuthorization();
        app.MapGet("/contributions/submissions/{id}", GetStatus).RequireAuthorization();
        app.MapGet("/contributions/submissions/{id}/file", GetFile).RequireAuthorization();

        return app;
    }

    private static async Task<IResult> PostSubmission(
        HttpRequest request, ClaimsPrincipal user, IConfiguration config, ContributionsStore store)
    {
        var caller = CallerIdentity.From(user, config);
        if (caller is null)
        {
            return Results.Forbid();
        }

        if (!request.HasFormContentType)
        {
            return Results.BadRequest("Expected multipart/form-data.");
        }

        if (request.ContentLength > MaxUploadBytes + MultipartOverheadBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var form = await request.ReadFormAsync();
        var file = form.Files["file"];
        if (file is null)
        {
            return Results.BadRequest("Expected a 'file' part.");
        }

        if (file.Length > MaxUploadBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        var resultCsv = ContributionsValidation.Validate(stream.ToArray());

        var id = Guid.NewGuid().ToString("N");
        store.Add(new ContributionsSubmission
        {
            Id = id,
            OwnerKey = caller.OwnerKey,
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

    private static IResult GetStatus(string id, ClaimsPrincipal user, IConfiguration config, ContributionsStore store)
    {
        var caller = CallerIdentity.From(user, config);
        var submission = caller is null ? null : store.Get(id, caller.OwnerKey);
        if (submission is null)
        {
            return Results.NotFound();
        }

        var status = DateTimeOffset.UtcNow >= submission.ReadyAt ? "processed" : "pending";
        return Results.Ok(new { id = submission.Id, status });
    }

    private static IResult GetFile(string id, ClaimsPrincipal user, IConfiguration config, ContributionsStore store)
    {
        var caller = CallerIdentity.From(user, config);
        var submission = caller is null ? null : store.Get(id, caller.OwnerKey);
        if (submission is null || DateTimeOffset.UtcNow < submission.ReadyAt)
        {
            return Results.NotFound();
        }

        return Results.File(submission.ResultCsvBytes, "text/csv", "contributions-response.csv", enableRangeProcessing: false);
    }
}
