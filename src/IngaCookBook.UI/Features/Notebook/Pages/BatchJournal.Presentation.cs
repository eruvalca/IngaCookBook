using IngaCookBook.SharedKernel.Notebook;
using Microsoft.AspNetCore.Components;

namespace IngaCookBook.UI.Features.Notebook.Pages;

public sealed partial class BatchJournal
{
    [SupplyParameterFromQuery(Name = "mode")] public string? Mode { get; set; }
    private string View { get; set; } = "batch";
    private JournalReceipt? _receipt;
    private ElementReference _receiptHeading;
    private bool _focusReceipt;
    private int RatedCount => _scores.Count(s => s.Score is >= 1 and <= 10);
    private string Heading => View switch { "batch" => "Make a batch", "history" => "Your tastings", _ => "How did it turn out?" };

    private void ShowView(string view)
    {
        // Switching tasks retains both unfinished forms and their navigation guard.
        View = view;
        _receipt = null;
        _focusBatch = string.Equals(view, "batch", StringComparison.Ordinal);
        _focusTasting = string.Equals(view, "taste", StringComparison.Ordinal) && _version?.Batches.Count > 0;
        Status = null;
        Error = null;
    }

    private Task TryIdeaAsync(Guid evaluationId) => RunAsync(async ct =>
    {
        if (_recipe is null || _navigationInterop is not null && !await _navigationInterop.ConfirmDiscardAsync(ct)) { return; }
        var result = await ReceiveAsync(Notebook.VaryAsync(RecipeId, VersionId, new(_recipe.Revision), ct), ct);
        if (Saved(result) && result is ChangeSaved saved)
        {
            // Creating the variation succeeded; navigating now must not prompt twice.
            _savedBatch = BatchState;
            _savedEvaluation = EvaluationState;
            _savedRevision++;
            if (_navigationInterop is not null) { await _navigationInterop.UpdateAsync(_editor, false, _savedRevision, ct); }
            Navigation.NavigateTo($"/recipes/{RecipeId}/versions/{saved.Id}/edit?idea={evaluationId}");
        }
    });

    // These distinct receipts carry the data needed by each completion screen.
    private abstract record JournalReceipt(DateTimeOffset RecordedAt);
    private sealed record BatchReceipt(Guid BatchId, int Number, DateTimeOffset MadeAt, string Notes) : JournalReceipt(MadeAt);
    private sealed record TastingReceipt(Guid Id, int Number, DateTimeOffset TastedAt, string Notes, string NextIdea, IReadOnlyList<MetricScore> Scores) : JournalReceipt(TastedAt);
}
