using IngaCookBook.Features.Installation;
using Microsoft.AspNetCore.Components;

namespace IngaCookBook.Components;

public sealed partial class App
{
    [Inject] private ApplicationRelease Release { get; set; } = default!;
}
