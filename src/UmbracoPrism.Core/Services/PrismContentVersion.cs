namespace UmbracoPrism.Core.Services;

/// <summary>
/// Changes whenever the site's pages may have changed: on every content publish or unpublish, and
/// on every start, since a deploy can change templates. A generated app with saved pages compares
/// it on a fresh copy of a page with the saved copy's, and swaps in the fresh copy only when they
/// differ. Held in memory, so each instance of a load-balanced site has its own; the worst that
/// does is swap in a copy that looks the same.
/// </summary>
public sealed class PrismContentVersion
{
    private readonly string _started = DateTime.UtcNow.Ticks.ToString("x");
    private long _changes;

    public string Current => $"{_started}.{Interlocked.Read(ref _changes)}";

    public void Advance() => Interlocked.Increment(ref _changes);
}
