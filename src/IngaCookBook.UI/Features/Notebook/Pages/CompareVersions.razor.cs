using System.Globalization;
using IngaCookBook.SharedKernel.Notebook;
using Microsoft.AspNetCore.Components;

namespace IngaCookBook.UI.Features.Notebook.Pages;

public sealed partial class CompareVersions
{
    [Parameter] public Guid RecipeId { get; set; }
    [SupplyParameterFromQuery(Name = "left")] public Guid? Left { get; set; }
    [SupplyParameterFromQuery(Name = "right")] public Guid? Right { get; set; }
    [SupplyParameterFromQuery(Name = "le")] public Guid? LeftEvaluation { get; set; }
    [SupplyParameterFromQuery(Name = "re")] public Guid? RightEvaluation { get; set; }
    private RecipeDocument? _recipe;
    private RecipeVersion? _left;
    private RecipeVersion? _right;
    private BatchEvaluation? _leftEvaluation;
    private BatchEvaluation? _rightEvaluation;
    private IReadOnlyList<RecipeDifference> _changes = [];
    private IEnumerable<EvaluationMetric> ObservedMetrics => _recipe?.Metrics.Where(HasObservation) ?? [];
    private IEnumerable<EvaluationMetric> UnassessedMetrics => _recipe?.Metrics.Where(m => !HasObservation(m)) ?? [];

    private bool HasObservation(EvaluationMetric metric) => new[] { _leftEvaluation, _rightEvaluation }
        .Any(e => e?.Scores.Any(s => s.MetricId == metric.Id && (s.Score is not null || !string.IsNullOrWhiteSpace(s.Notes))) == true);

    protected override Task OnParametersSetAsync() => LoadAsync(async ct =>
    {
        _recipe = await ReceiveAsync(Notebook.GetRecipeAsync(RecipeId, ct), ct);
        if (_recipe is null || _recipe.Versions.Count == 0) { return; }
        _right = _recipe.Versions.FirstOrDefault(v => v.Id == Right) ?? _recipe.Versions[^1];
        _left = _recipe.Versions.FirstOrDefault(v => v.Id == Left)
            ?? _recipe.Versions.FirstOrDefault(v => v.Id == _right.ParentId) ?? _recipe.Versions[0];
        _changes = RecipeComparison.Compare(_left.Content, _right.Content);
        _leftEvaluation = SelectEvaluation(_left, LeftEvaluation);
        _rightEvaluation = SelectEvaluation(_right, RightEvaluation);
    });

    private static BatchEvaluation? SelectEvaluation(RecipeVersion version, Guid? id) =>
        version.Batches.SelectMany(b => b.Evaluations).FirstOrDefault(e => e.Id == id) ?? RecipeComparison.LatestEvaluation(version);

    private static IEnumerable<EvaluationOption> EvaluationOptions(RecipeVersion version) => version.Batches.SelectMany((b, i) =>
        b.Evaluations.Select((e, j) => new EvaluationOption(e.Id, $"Batch {i + 1} ({b.MadeAt:MMM d}) · tasting {j + 1} ({e.TastedAt:MMM d, yyyy})")));

    private static string EvaluationLabel(RecipeVersion version, BatchEvaluation? evaluation) =>
        evaluation is null ? $"V{version.Number}: no tasting" : $"V{version.Number}: {EvaluationOptions(version).First(e => e.Id == evaluation.Id).Label}";

    private static string Delta(int? left, int? right) => left is not null && right is not null
        ? (right.Value - left.Value).ToString("+0;-0;0", CultureInfo.CurrentCulture) : "—";

    private sealed record EvaluationOption(Guid Id, string Label);
}
