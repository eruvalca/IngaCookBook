using IngaCookBook.SharedKernel.Notebook;
using Microsoft.AspNetCore.Components;

namespace IngaCookBook.UI.Features.Notebook.Components;

public sealed partial class EvaluationAudit
{
    [Parameter, EditorRequired] public IReadOnlyList<EvaluationCorrection> Corrections { get; set; } = [];
    [Parameter, EditorRequired] public IReadOnlyList<EvaluationMetric> Metrics { get; set; } = [];
}
