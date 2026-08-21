using System.Collections.Concurrent;

namespace UmbracoPrism.MockBusinessApp.Services.SupportSystem;

/// <summary>
/// In-memory backing store for contributions-file submissions — kept separate from
/// <see cref="SupportSystemStore"/> deliberately: that one models a human-decided submission
/// (JSON status, staff approve/reject), this one models a whole file validated automatically and
/// handed back annotated. The two request/response shapes don't share enough to be worth forcing
/// into one model — see <see cref="ContributionsEndpoints"/>'s own remarks.
/// </summary>
public sealed class ContributionsStore
{
    private readonly ConcurrentDictionary<string, ContributionsSubmission> _submissions = new(StringComparer.Ordinal);

    public ContributionsSubmission Add(ContributionsSubmission submission)
    {
        _submissions[submission.Id] = submission;
        return submission;
    }

    public ContributionsSubmission? Get(string id) => _submissions.GetValueOrDefault(id);
}

public sealed record ContributionsSubmission
{
    public required string Id { get; init; }
    public DateTimeOffset SubmittedAt { get; init; }
    public DateTimeOffset ReadyAt { get; init; }
    public required byte[] ResultCsvBytes { get; init; }
}
