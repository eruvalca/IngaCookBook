using System.Globalization;
using Microsoft.JSInterop;

namespace IngaCookBook.UI.Features.Notebook;

internal sealed class BrowserDateInterop(IJSRuntime runtime) : IAsyncDisposable
{
    internal const string ModulePath = "./_content/IngaCookBook.UI/Features/Notebook/Pages/BatchJournal.razor.js";
    private IJSObjectReference? _module;

    internal async Task<DateTime> GetTodayAsync()
    {
        _module ??= await runtime.InvokeAsync<IJSObjectReference>("import", ModulePath);
        var date = await _module.InvokeAsync<string>("localDate");
        return DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try { await _module.DisposeAsync(); }
            catch (JSDisconnectedException) { /* The circuit already ended. */ }
        }
    }
}
