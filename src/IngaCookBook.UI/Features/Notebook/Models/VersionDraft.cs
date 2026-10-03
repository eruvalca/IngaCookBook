using IngaCookBook.SharedKernel.Notebook;

namespace IngaCookBook.UI.Features.Notebook.Models;

internal sealed class VersionDraft
{
    public string Label { get; set; } = "";
    public string Notes { get; set; } = "";
    public string TargetMetric { get; set; } = "";
    public string Hypothesis { get; set; } = "";
    public string RelatedChanges { get; set; } = "";
    public decimal? Yield { get; set; }
    public string YieldUnit { get; set; } = "g";
    public List<IngredientInput> Ingredients { get; set; } = [];
    public List<StepInput> Steps { get; set; } = [];

    public RecipeContent ToContent() => new()
    {
        Label = Label,
        Notes = Notes,
        TargetMetricId = Guid.TryParse(TargetMetric, out var id) ? id : null,
        Hypothesis = Hypothesis,
        RelatedChanges = RelatedChanges,
        Yield = Yield,
        YieldUnit = YieldUnit,
        Ingredients = Ingredients.Select(i => i.ToIngredient()).ToArray(),
        Steps = Steps.Select(s => new PreparationStep(s.Id, s.Instruction, s.Notes)).ToArray(),
    };

    public static VersionDraft From(RecipeContent content) => new()
    {
        Label = content.Label,
        Notes = content.Notes,
        TargetMetric = content.TargetMetricId?.ToString() ?? "",
        Hypothesis = content.Hypothesis,
        RelatedChanges = content.RelatedChanges,
        Yield = content.Yield,
        YieldUnit = content.YieldUnit,
        Ingredients = content.Ingredients.Select(IngredientInput.From).ToList(),
        Steps = content.Steps.Select(s => new StepInput { Id = s.Id, Instruction = s.Instruction, Notes = s.Notes }).ToList(),
    };
}
