using IngaCookBook.SharedKernel.Notebook;

namespace IngaCookBook.Features.Notebook.Services;

internal static class NotebookValidation
{
    internal static string? Settings(string? name, string? description, IReadOnlyList<EvaluationMetric>? metrics)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || description is null || description.Length > 4000)
        {
            return "Enter a recipe name (up to 200 characters) and a description of up to 4,000 characters.";
        }
        if (metrics is null || metrics.Any(m => m is null || m.Id == Guid.Empty || string.IsNullOrWhiteSpace(m.Name) || m.Name.Length > 100))
        {
            return "Each evaluation metric needs a name of up to 100 characters.";
        }
        if (metrics.Select(m => m.Id).Distinct().Count() != metrics.Count ||
            metrics.Select(m => m.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != metrics.Count)
        {
            return "Give each evaluation metric a unique name.";
        }
        return null;
    }

    internal static string? Content(RecipeContent? content, bool complete)
    {
        if (content is null || string.IsNullOrWhiteSpace(content.Label) || content.Label.Length > 200)
        {
            return "Give this version a short label.";
        }
        if (content.Ingredients is null || content.Steps is null || content.Notes is null ||
            content.Hypothesis is null || content.RelatedChanges is null)
        {
            return "The recipe data is incomplete.";
        }
        if (content.Ingredients.Any(i => i is null || i.Id == Guid.Empty || i.Name is null || i.Name.Length > 300 ||
                !RecipeCosting.Units.Contains(i.Unit, StringComparer.Ordinal) || !RecipeCosting.Units.Contains(i.PurchaseUnit, StringComparer.Ordinal)))
        {
            return "Check ingredient names and select supported units.";
        }
        if (content.Ingredients.Any(i => i.Quantity is <= 0 or > 1000000000 ||
                i.PurchaseQuantity is <= 0 or > 1000000000 || i.PurchasePrice is < 0 or > 1000000000))
        {
            return "Quantities must be positive and prices cannot be negative (maximum 1,000,000,000).";
        }
        return Structure(content, complete);
    }

    private static string? Structure(RecipeContent content, bool complete)
    {
        if (content.Steps.Any(s => s is null || s.Id == Guid.Empty || s.Instruction is null || s.Notes is null) ||
            content.Ingredients.Select(i => i.Id).Distinct().Count() != content.Ingredients.Count ||
            content.Steps.Select(s => s.Id).Distinct().Count() != content.Steps.Count)
        {
            return "Ingredient and step rows must be unique and contain valid text.";
        }
        if (content.Yield is <= 0 or > 1000000000 || !RecipeCosting.Units.Contains(content.YieldUnit, StringComparer.Ordinal))
        {
            return "Enter a positive yield and a supported yield unit.";
        }
        if (complete && (content.Ingredients.Count == 0 || content.Steps.Count == 0 ||
            content.Ingredients.Any(i => string.IsNullOrWhiteSpace(i.Name) || i.Quantity is null) ||
            content.Steps.Any(s => string.IsNullOrWhiteSpace(s.Instruction))))
        {
            return "Before making a batch or choosing a standard, add ingredients with quantities and at least one preparation step.";
        }
        return null;
    }

    internal static string? Evaluation(RecipeDocument recipe, EvaluationRequest request)
    {
        if (request.Scores is null || request.Notes is null || request.NextIdea is null ||
            request.Scores.Any(s => s is null || s.Notes is null || s.Score is < 1 or > 10 ||
                !recipe.Metrics.Any(m => m.Id == s.MetricId)) ||
            request.Scores.Select(s => s.MetricId).Distinct().Count() != request.Scores.Count)
        {
            return "Scores must be whole numbers from 1 to 10 for the recipe's current metrics. Leave untested metrics blank.";
        }
        if (!request.Scores.Any(s => s.Score is not null || !string.IsNullOrWhiteSpace(s.Notes)) &&
            string.IsNullOrWhiteSpace(request.Notes) && string.IsNullOrWhiteSpace(request.NextIdea))
        {
            return "Add at least one score or observation before saving an evaluation.";
        }
        return request.TastedAt == default ? "Choose a tasting date." : null;
    }
}
