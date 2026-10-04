using Microsoft.AspNetCore.Components;

namespace IngaCookBook.UI;

/// <summary>Owns cancellation and a single asynchronous cleanup path for components that do work.</summary>
public abstract class CancellableComponentBase : ComponentBase, IAsyncDisposable
{
    private CancellationTokenSource? _lifetime;
    protected bool IsDisposed { get; private set; }
    protected CancellationToken LifetimeToken => IsDisposed
        ? new CancellationToken(canceled: true)
        : (_lifetime ??= new()).Token;

    public async ValueTask DisposeAsync()
    {
        if (IsDisposed) { return; }
        IsDisposed = true;
        try
        {
            if (_lifetime is not null) { await _lifetime.CancelAsync(); }
        }
        finally
        {
            try { await DisposeCoreAsync(); }
            finally { _lifetime?.Dispose(); }
        }
        GC.SuppressFinalize(this);
    }

    // Derived components release JS references here rather than hiding DisposeAsync.
    protected virtual ValueTask DisposeCoreAsync() => ValueTask.CompletedTask;
}
