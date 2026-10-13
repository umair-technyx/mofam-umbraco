namespace Mofam.Application.Services;

// One shared token for all cached pages. Every cached page is linked to it, so calling Reset
// removes all of them at once, without needing to know their keys. It is a singleton.
public sealed class PageCacheToken
{
    private CancellationTokenSource _source = new();

    public CancellationToken Token => _source.Token;

    // Expires every cache entry linked to the current token and starts a new one.
    public void Reset()
    {
        var old = Interlocked.Exchange(ref _source, new CancellationTokenSource());
        old.Cancel();
        old.Dispose();
    }
}
