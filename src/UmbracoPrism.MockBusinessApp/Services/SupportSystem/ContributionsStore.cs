using System.Collections.Concurrent;

namespace UmbracoPrism.MockBusinessApp.Services.SupportSystem;

/// <summary>
/// In-memory backing store for contributions-file submissions, kept for the process lifetime. Every
/// submission belongs to the caller who made it (<see cref="CallerIdentity.OwnerKey"/>) and can only
/// be read back by that caller, so knowing or guessing an id grants nothing. The store is capped,
/// evicting the oldest, so an authenticated caller cannot grow it without bound.
/// </summary>
public sealed class ContributionsStore
{
    public const int MaxSubmissions = 200;

    private readonly ConcurrentDictionary<string, ContributionsSubmission> _submissions = new(StringComparer.Ordinal);

    public ContributionsSubmission Add(ContributionsSubmission submission)
    {
        _submissions[submission.Id] = submission;

        while (_submissions.Count > MaxSubmissions)
        {
            var oldest = _submissions.Values.MinBy(s => s.SubmittedAt);
            if (oldest is null || !_submissions.TryRemove(oldest.Id, out _))
            {
                break;
            }
        }

        return submission;
    }

    /// <summary>
    /// The submission, only if <paramref name="ownerKey"/> made it. An unknown id and someone else's
    /// id are indistinguishable to the caller, so ids cannot be probed.
    /// </summary>
    public ContributionsSubmission? Get(string id, string ownerKey) =>
        _submissions.GetValueOrDefault(id) is { } submission
        && string.Equals(submission.OwnerKey, ownerKey, StringComparison.Ordinal)
            ? submission
            : null;
}

public sealed record ContributionsSubmission
{
    public required string Id { get; init; }
    public required string OwnerKey { get; init; }
    public DateTimeOffset SubmittedAt { get; init; }
    public DateTimeOffset ReadyAt { get; init; }
    public required byte[] ResultCsvBytes { get; init; }
}
