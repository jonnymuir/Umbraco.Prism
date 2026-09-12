using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace UmbracoPrism.Core.IntegrationTests;

/// <summary>
/// An <see cref="IStartupFilter"/> that wraps the ENTIRE app pipeline (registered before the
/// real <c>Program.cs</c> pipeline is built, so it composes correctly regardless of whether the
/// app uses a classic Startup class or minimal-hosting top-level statements) in a try/catch,
/// capturing the raw exception object — unstripped, unlogged, un-filtered by any category or
/// minimum-level configuration — for a test to inspect directly.
///
/// Exists because <see cref="HostErrorLogCapture"/> (which listens for Error/Critical-level
/// <see cref="Microsoft.Extensions.Logging.ILogger"/> entries) caught nothing across two separate
/// occurrences of BiometricController.Exchange's CI-only 500 in a row — proving the exception
/// either isn't logged via <c>ILogger</c> at all on this path, or is logged below Error level.
/// This bypasses logging entirely: whatever throws, this sees it, guaranteed.
/// </summary>
public sealed class RawExceptionCapture : IStartupFilter
{
    private readonly ConcurrentQueue<Exception> _exceptions = new();

    /// <summary>Removes and returns every exception captured since the last drain.</summary>
    public IReadOnlyList<Exception> Drain()
    {
        var drained = new List<Exception>();
        while (_exceptions.TryDequeue(out var ex))
        {
            drained.Add(ex);
        }

        return drained;
    }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            try
            {
                await nextMiddleware();
            }
            catch (Exception ex)
            {
                _exceptions.Enqueue(ex);
                throw; // rethrow: don't change real request behaviour, only observe it.
            }
        });

        next(app);
    };
}
