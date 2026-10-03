using System.Globalization;
using Microsoft.FluentUI.AspNetCore.Components;

namespace IngaCookBook.UI.Features.Notebook.Models;

// Keep the Fluent mask's six-place precision while formatting optional decimal
// places. The pinned component uses string.Format(Culture, "{0:N}", value).
internal sealed class QuantityInputCulture() : FluentNumberInputCultureInfo(6), ICustomFormatter
{
    public override object? GetFormat(Type? formatType) => formatType == typeof(ICustomFormatter) ? this : base.GetFormat(formatType);

    public string Format(string? format, object? arg, IFormatProvider? formatProvider) => arg switch
    {
        null => "",
        decimal value => value.ToString("0.######", this),
        IFormattable value => value.ToString(format, CultureInfo.InvariantCulture),
        _ => arg.ToString() ?? "",
    };
}
