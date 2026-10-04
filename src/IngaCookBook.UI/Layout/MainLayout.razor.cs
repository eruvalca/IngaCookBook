using Microsoft.AspNetCore.Components;

namespace IngaCookBook.UI.Layout;

public sealed partial class MainLayout(NavigationManager navigation)
{
    private string MainContentUrl => new UriBuilder(navigation.Uri) { Fragment = "main-content" }.Uri.AbsoluteUri;
}
