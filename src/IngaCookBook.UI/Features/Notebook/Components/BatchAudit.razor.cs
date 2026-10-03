using IngaCookBook.SharedKernel.Notebook;
using Microsoft.AspNetCore.Components;

namespace IngaCookBook.UI.Features.Notebook.Components;

public sealed partial class BatchAudit
{
    [Parameter, EditorRequired] public IReadOnlyList<BatchCorrection> Corrections { get; set; } = [];
}
