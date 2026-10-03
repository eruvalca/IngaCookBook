namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>Estimates costs within compatible units; US customary volume definitions are explicit.</summary>
public static class RecipeCosting
{
    public static IReadOnlyList<string> Units { get; } = ["g", "kg", "oz", "lb", "mL", "L", "tsp (US)", "tbsp (US)", "cup (US)", "fl oz (US)", "count"];

    public static IngredientCost Calculate(RecipeContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        decimal total = 0;
        var missing = 0;
        foreach (var ingredient in content.Ingredients)
        {
            var cost = CalculateIngredient(ingredient);
            if (cost is { } amount)
            {
                total += amount;
            }
            else
            {
                missing++;
            }
        }
        // Nested recipes consume this value too. Round only when displaying the final estimate.
        return new(total, missing);
    }

    public static decimal? CalculateIngredient(Ingredient ingredient)
    {
        ArgumentNullException.ThrowIfNull(ingredient);
        var quantity = ingredient.PurchaseQuantity;
        var price = ingredient.PurchasePrice;
        var unit = ingredient.PurchaseUnit;
        if (ingredient.LinkedContent is { } nested)
        {
            var nestedCost = Calculate(nested);
            quantity = nested.Yield;
            unit = nested.YieldUnit;
            price = nestedCost.MissingCount == 0 ? nestedCost.KnownTotal : null;
        }
        if (ingredient.Quantity is not > 0 || quantity is not > 0 || price is null or < 0)
        {
            return null;
        }
        var usedUnit = Measure(ingredient.Unit);
        var purchasedUnit = Measure(unit);
        return usedUnit.Family != 0 && usedUnit.Family == purchasedUnit.Family
            ? ingredient.Quantity * usedUnit.Factor / (quantity * purchasedUnit.Factor) * price
            : null;
    }

    private static (int Family, decimal Factor) Measure(string unit) => unit switch
    {
        "g" => (1, 1),
        "kg" => (1, 1000),
        "oz" => (1, 28.349523125m),
        "lb" => (1, 453.59237m),
        "mL" => (2, 1),
        "L" => (2, 1000),
        "tsp (US)" => (2, 4.92892159375m),
        "tbsp (US)" => (2, 14.78676478125m),
        "cup (US)" => (2, 236.5882365m),
        "fl oz (US)" => (2, 29.5735295625m),
        "count" => (3, 1),
        _ => (0, 0),
    };
}
