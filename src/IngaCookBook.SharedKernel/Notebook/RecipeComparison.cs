using System.Globalization;

namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>Explains changes using stable row identities rather than text matching.</summary>
public static class RecipeComparison
{
    public static IReadOnlyList<RecipeDifference> Compare(RecipeContent before, RecipeContent after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var changes = new List<RecipeDifference>();
        foreach (var id in before.Ingredients.Select(i => i.Id).Union(after.Ingredients.Select(i => i.Id)))
        {
            var left = before.Ingredients.FirstOrDefault(i => i.Id == id);
            var right = after.Ingredients.FirstOrDefault(i => i.Id == id);
            if (IngredientChanged(left, right))
            {
                changes.Add(new("Ingredient", right?.Name ?? left?.Name ?? "Ingredient", Describe(left), Describe(right)));
            }
        }
        foreach (var id in before.Steps.Select(s => s.Id).Union(after.Steps.Select(s => s.Id)))
        {
            var left = before.Steps.FirstOrDefault(s => s.Id == id);
            var right = after.Steps.FirstOrDefault(s => s.Id == id);
            var leftIndex = before.Steps.ToList().FindIndex(s => s.Id == id);
            var rightIndex = after.Steps.ToList().FindIndex(s => s.Id == id);
            if (left != right || leftIndex != rightIndex)
            {
                changes.Add(new("Preparation", $"Step {(rightIndex < 0 ? leftIndex : rightIndex) + 1}", Describe(left, leftIndex), Describe(right, rightIndex)));
            }
        }
        if (before.Yield != after.Yield || !string.Equals(before.YieldUnit, after.YieldUnit, StringComparison.Ordinal))
        {
            changes.Add(new("Yield", "Expected yield", $"{before.Yield} {before.YieldUnit}", $"{after.Yield} {after.YieldUnit}"));
        }
        return changes;
    }

    public static BatchEvaluation? LatestEvaluation(RecipeVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return version.Batches.SelectMany(b => b.Evaluations).MaxBy(e => (e.TastedAt, e.RecordedAt));
    }

    private static bool IngredientChanged(Ingredient? left, Ingredient? right) =>
        !string.Equals(left?.Name, right?.Name, StringComparison.Ordinal) ||
        left?.Quantity != right?.Quantity || !string.Equals(left?.Unit, right?.Unit, StringComparison.Ordinal) ||
        left?.RecipeId != right?.RecipeId || left?.VersionId != right?.VersionId;

    private static string Describe(Ingredient? item)
    {
        if (item is null)
        {
            return "—";
        }
        var linked = item.VersionId is null ? "" : $" · {item.LinkedContent?.Label ?? "linked recipe"}";
        return $"{item.Name}: {item.Quantity?.ToString("0.############################", CultureInfo.CurrentCulture) ?? "Unspecified"} {item.Unit}{linked}";
    }

    private static string Describe(PreparationStep? step, int index) =>
        step is null ? "—" : $"{index + 1}. {step.Instruction} {step.Notes}".Trim();
}
