using System.Net;
using System.Text.Encodings.Web;
using Shouldly;

namespace IngaCookBook.PlaywrightTests;

internal static class AccountInbox
{
    public static async Task<string> ReadLinkAsync(BrowserAppFixture application, string recipient, string path, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var client = new HttpClient { BaseAddress = application.InboxEndpoint };
        using var poll = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        // Every test has its own recipient. Never clear or read the unfiltered shared inbox.
        var query = Uri.EscapeDataString($"to:{recipient}");
        do
        {
            using var response = await client.GetAsync(new Uri($"/view/latest.txt?query={query}", UriKind.Relative), timeout.Token);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                continue;
            }
            response.EnsureSuccessStatusCode();
            var text = await response.Content.ReadAsStringAsync(timeout.Token);
            var link = text.Split('\n', StringSplitOptions.TrimEntries).SingleOrDefault(line => line.StartsWith("https://", StringComparison.Ordinal));
            if (link is null || !string.Equals(new Uri(link).AbsolutePath, path, StringComparison.Ordinal))
            {
                continue;
            }
            new Uri(link).GetLeftPart(UriPartial.Authority).ShouldBe(application.Endpoint.GetLeftPart(UriPartial.Authority));
            var html = await client.GetStringAsync(new Uri($"/view/latest.html?query={query}", UriKind.Relative), timeout.Token);
            html.ShouldContain($"href=\"{HtmlEncoder.Default.Encode(link)}\"");
            text.ShouldContain("If you did not request this");
            return link;
        } while (await poll.WaitForNextTickAsync(timeout.Token));
        throw new InvalidOperationException("Mailpit did not capture the expected account email.");
    }
}
