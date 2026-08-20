using System.Collections.Concurrent;

namespace UmbracoPrism.MockBusinessApp.Services.SupportSystem;

/// <summary>In-memory backing store for <see cref="SupportSystemSubmission"/> — process-lifetime only, matching this demo app's own "no real persistence" convention elsewhere.</summary>
public sealed class SupportSystemStore
{
    private readonly ConcurrentDictionary<string, SupportSystemSubmission> _submissions = new(StringComparer.Ordinal);

    public SupportSystemSubmission Add(SupportSystemSubmission submission)
    {
        _submissions[submission.Id] = submission;
        return submission;
    }

    public SupportSystemSubmission? Get(string id) => _submissions.GetValueOrDefault(id);

    public IReadOnlyList<SupportSystemSubmission> GetPending() =>
        _submissions.Values.Where(s => !s.Decided).OrderBy(s => s.SubmittedAt).ToArray();

    public SupportSystemSubmission? Decide(string id, string outcomeKey)
    {
        if (!_submissions.TryGetValue(id, out var existing) || existing.Decided)
        {
            return null;
        }

        var decided = existing with { Decided = true, OutcomeKey = outcomeKey, DecidedAt = DateTimeOffset.UtcNow };
        _submissions[id] = decided;
        return decided;
    }
}
