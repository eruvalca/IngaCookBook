using IngaCookBook.SharedKernel.Notebook;
using Microsoft.AspNetCore.Components;

namespace IngaCookBook.UI.Features.Notebook.Pages;

public sealed partial class BatchJournal
{
    private Guid? _correctingBatchId;
    private string _batchCorrectionReason = "";
    private DateTime? _recordingDate;
    private ElementReference _batchHeading;
    private bool _focusBatch;

    private async Task CorrectBatchAsync(RecipeBatch batch)
    {
        if (BatchDirty && _navigationInterop is not null && !await _navigationInterop.ConfirmDiscardAsync()) { return; }
        View = "batch";
        _receipt = null;
        if (_correctingBatchId is null)
        {
            _recordingDate = _madeDate;
        }
        _correctingBatchId = batch.Id;
        _madeDate = batch.MadeAt.Date;
        _batchNotes = batch.Notes;
        _batchCorrectionReason = "";
        _savedBatch = BatchState;
        _savedRevision++;
        _focusBatch = true;
        Error = null;
        Status = null;
    }

    private async Task CancelBatchCorrectionAsync()
    {
        if (BatchDirty && _navigationInterop is not null && !await _navigationInterop.ConfirmDiscardAsync()) { return; }
        ResetBatchCorrection();
    }

    private void ResetBatchCorrection()
    {
        _correctingBatchId = null;
        _madeDate = _recordingDate;
        _batchNotes = "";
        _batchCorrectionReason = "";
        _savedBatch = BatchState;
        _savedRevision++;
    }
}
