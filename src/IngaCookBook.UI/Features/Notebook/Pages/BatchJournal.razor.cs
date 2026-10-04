using System.Globalization;
using System.Text.Json;
using IngaCookBook.SharedKernel.Notebook;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace IngaCookBook.UI.Features.Notebook.Pages;

public sealed partial class BatchJournal
{
    [Parameter] public Guid RecipeId { get; set; }
    [Parameter] public Guid VersionId { get; set; }
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    private RecipeDocument? _recipe;
    private RecipeVersion? _version;
    private DateTime? _madeDate;
    private DateTime? _tastedDate;
    private BrowserDateInterop? _dateInterop;
    private EditorNavigationInterop? _navigationInterop;
    private ElementReference _editor;
    private ElementReference _tastingHeading;
    private bool _focusTasting;
    private bool _baselinesReady;
    private int _savedRevision;
    private string _savedBatch = "";
    private string _savedEvaluation = "";
    private Guid? _correctingId;
    private string _correctionReason = "";
    private bool GuardReady { get; set; }

    private bool DatesReady { get; set; }
    private bool JournalDisabled => Disabled || !DatesReady || !GuardReady || !_baselinesReady;
    private string BatchState => JsonSerializer.Serialize(new { _madeDate, _batchNotes, _batchCorrectionReason });
    private string EvaluationState => JsonSerializer.Serialize(new { _batchId, _tastedDate, _notes, _nextIdea, _scores, _correctionReason });
    private bool EvaluationDirty => _baselinesReady && !string.Equals(_savedEvaluation, EvaluationState, StringComparison.Ordinal);
    private bool BatchDirty => _baselinesReady && !string.Equals(_savedBatch, BatchState, StringComparison.Ordinal);
    private bool Dirty => BatchDirty || EvaluationDirty;
    private string _batchNotes = "";
    private string _batchId = "";
    private string _notes = "";
    private string _nextIdea = "";
    private List<ScoreInput> _scores = [];
    private IEnumerable<BatchOption> BatchOptions => _version?.Batches.Select((b, i) =>
        new BatchOption(b.Id.ToString(), $"Batch {i + 1} · {b.MadeAt:MMM d, yyyy}")) ?? [];

    protected override Task OnParametersSetAsync() => LoadAsync(async ct =>
    {
        _recipe = null;
        _version = null;
        _baselinesReady = false;
        _batchId = "";
        _batchNotes = "";
        _correctingBatchId = null;
        _batchCorrectionReason = "";
        ResetEvaluation();
        await ReloadAsync(ct);
        _scores = _recipe?.Metrics.Select(m => new ScoreInput { Id = m.Id, Name = m.Name }).ToList() ?? [];
        View = string.Equals(Mode, "make", StringComparison.Ordinal) || _version?.Batches.Count == 0 ? "batch" : "taste";
        _receipt = null;
    });

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dateInterop = new BrowserDateInterop(JavaScript);
            var today = await _dateInterop.GetTodayAsync(LifetimeToken);
            _madeDate ??= today;
            _tastedDate ??= today;
            DatesReady = true;
            StateHasChanged();
        }
        if (DatesReady && !Busy && _version is not null && !_baselinesReady)
        {
            _savedBatch = BatchState;
            _savedEvaluation = EvaluationState;
            _baselinesReady = true;
            StateHasChanged();
        }
        _navigationInterop ??= new EditorNavigationInterop(JavaScript);
        await _navigationInterop.UpdateAsync(_editor, Dirty, _savedRevision, LifetimeToken);
        if (!GuardReady)
        {
            GuardReady = true;
            StateHasChanged();
        }
        if (_focusTasting)
        {
            _focusTasting = false;
            await _tastingHeading.FocusAsync();
        }
        if (_focusBatch)
        {
            _focusBatch = false;
            await _batchHeading.FocusAsync();
        }
        if (_focusReceipt)
        {
            _focusReceipt = false;
            await _receiptHeading.FocusAsync();
        }
    }

    private async Task ReloadAsync(CancellationToken ct)
    {
        _recipe = await ReceiveAsync(Notebook.GetRecipeAsync(RecipeId, ct), ct);
        _version = _recipe?.Versions.FirstOrDefault(v => v.Id == VersionId);
        if (string.IsNullOrEmpty(_batchId) && _version?.Batches.Count > 0)
        {
            _batchId = (_version.Batches.LastOrDefault(b => b.Evaluations.Count == 0) ?? _version.Batches[^1]).Id.ToString();
        }
    }

    private Task MakeAsync() => RunAsync(async ct =>
    {
        if (_recipe is null || _madeDate is not { } madeDate) { return; }
        var request = new BatchRequest(_recipe.Revision, Date(madeDate), _batchNotes);
        var result = _correctingBatchId is { } batchId
            ? await ReceiveAsync(Notebook.CorrectBatchAsync(RecipeId, VersionId, batchId, new(request, _batchCorrectionReason), ct), ct)
            : await ReceiveAsync(Notebook.MakeBatchAsync(RecipeId, VersionId, request, ct), ct);
        if (Saved(result) && result is ChangeSaved saved)
        {
            var wasCorrection = _correctingBatchId is not null;
            var recordedNotes = _batchNotes;
            var selectRecordedBatch = !EvaluationDirty && _correctingId is null;
            await ReloadAsync(ct);
            if (_correctingBatchId is not null)
            {
                ResetBatchCorrection();
            }
            else if (selectRecordedBatch)
            {
                _batchId = saved.Id.ToString();
                _savedEvaluation = EvaluationState;
            }
            _batchNotes = "";
            _savedBatch = BatchState;
            _savedRevision++;
            if (!wasCorrection)
            {
                _receipt = new BatchReceipt(saved.Id, (_version?.Batches.ToList().FindIndex(b => b.Id == saved.Id) ?? -1) + 1, request.MadeAt, recordedNotes);
                _focusReceipt = true;
            }
        }
    });

    private Task EvaluateAsync() => RunAsync(async ct =>
    {
        if (_recipe is null || _tastedDate is not { } tastedDate || !Guid.TryParse(_batchId, out var batchId)) { return; }
        if (_scores.Any(s => !s.Valid))
        {
            Error = "Scores must be whole numbers from 1 to 10. Correct the highlighted scores, or leave them blank.";
            return;
        }
        var request = new EvaluationRequest(_recipe.Revision, Date(tastedDate), _notes, _nextIdea,
            _scores.Select(s => new MetricScore(s.Id, s.Score, s.Notes)).ToArray());
        var result = _correctingId is { } evaluationId
            ? await ReceiveAsync(Notebook.CorrectEvaluationAsync(RecipeId, VersionId, batchId, evaluationId, new(request, _correctionReason), ct), ct)
            : await ReceiveAsync(Notebook.EvaluateAsync(RecipeId, VersionId, batchId, request, ct), ct);
        if (Saved(result) && result is ChangeSaved saved)
        {
            await ReloadAsync(ct);
            _receipt = new TastingReceipt(saved.Id, (_version?.Batches.ToList().FindIndex(b => b.Id == batchId) ?? -1) + 1, request.TastedAt, request.Notes, request.NextIdea, request.Scores);
            ResetEvaluation();
            _focusReceipt = true;
        }
    });

    private async Task CorrectAsync(Guid batchId, BatchEvaluation evaluation)
    {
        if (EvaluationDirty && _navigationInterop is not null && !await _navigationInterop.ConfirmDiscardAsync(LifetimeToken)) { return; }
        View = "taste";
        _receipt = null;
        _correctingId = evaluation.Id;
        _batchId = batchId.ToString();
        _tastedDate = evaluation.TastedAt.Date;
        _notes = evaluation.Notes;
        _nextIdea = evaluation.NextIdea;
        _correctionReason = "";
        foreach (var score in _scores)
        {
            var saved = evaluation.Scores.FirstOrDefault(s => s.MetricId == score.Id);
            score.Text = saved?.Score?.ToString(CultureInfo.InvariantCulture) ?? "";
            score.Notes = saved?.Notes ?? "";
        }
        _savedEvaluation = EvaluationState;
        _savedRevision++;
        _focusTasting = true;
        Error = null;
        Status = null;
    }

    private async Task CancelCorrectionAsync()
    {
        if (EvaluationDirty && _navigationInterop is not null && !await _navigationInterop.ConfirmDiscardAsync(LifetimeToken)) { return; }
        ResetEvaluation();
    }

    private void ResetEvaluation()
    {
        _correctingId = null;
        _correctionReason = "";
        _notes = "";
        _nextIdea = "";
        foreach (var score in _scores) { score.Text = ""; score.Notes = ""; }
        _savedEvaluation = EvaluationState;
        _savedRevision++;
    }

    private async Task BeforeNavigateAsync(LocationChangingContext context)
    {
        if (_navigationInterop is not null && !await _navigationInterop.ConfirmDiscardAsync(LifetimeToken))
        {
            context.PreventNavigation();
        }
    }

    private static DateTimeOffset Date(DateTime value) => new(DateTime.SpecifyKind(value.Date, DateTimeKind.Utc));

    protected override async ValueTask DisposeCoreAsync()
    {
        await base.DisposeCoreAsync();
        if (_navigationInterop is not null)
        {
            await _navigationInterop.DisposeAsync();
        }
        if (_dateInterop is not null)
        {
            await _dateInterop.DisposeAsync();
        }
    }

    private sealed record BatchOption(string Id, string Label);
    private sealed class ScoreInput
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string Text { get; set; } = "";
        public int? Score => int.TryParse(Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
        public bool Valid => string.IsNullOrWhiteSpace(Text) || Score is >= 1 and <= 10;
        public string Notes { get; set; } = "";
    }
}
