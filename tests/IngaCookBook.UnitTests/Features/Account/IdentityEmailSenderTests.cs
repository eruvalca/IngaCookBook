using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;
using IngaCookBook.Data;
using IngaCookBook.Features.Account.Services.Email;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace IngaCookBook.UnitTests.Features.Account;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class IdentityEmailSenderTests
{
    [Theory]
    [InlineData(false, "Confirm your email", "Confirm email address")]
    [InlineData(true, "Reset your password", "Reset password")]
    public async Task MessagesPreserveEncodedTokensInBothBodyFormats(bool reset, string subject, string action)
    {
        const string Link = "https://localhost/Account/ConfirmEmail?userId=member%2Fid&code=token%2B%2F%3D&returnUrl=%2Frecipes%3Fa%3D1%26b%3D2";
        var transport = Substitute.For<IAccountEmailTransport>();
        var sender = Create(transport);
        // A change-email confirmation must go to the requested address, not the old user email.
        var user = new ApplicationUser { Email = "old@example.test" };
        if (reset)
        {
            await sender.SendPasswordResetLinkAsync(user, "new@example.test", HtmlEncoder.Default.Encode(Link));
        }
        else
        {
            await sender.SendConfirmationLinkAsync(user, "new@example.test", HtmlEncoder.Default.Encode(Link));
        }
        await transport.Received(1).SendAsync(Arg.Is<AccountEmailMessage>(message =>
            message.Recipient == "new@example.test" && message.Subject == subject &&
            message.Text.Contains(Link, StringComparison.Ordinal) &&
            message.Html.Contains($"href=\"{HtmlEncoder.Default.Encode(Link)}\"", StringComparison.Ordinal) &&
            message.Html.Contains(action, StringComparison.Ordinal) &&
            !message.Html.Contains("&amp;amp;", StringComparison.Ordinal)), Arg.Is<CancellationToken>(ct => ct.CanBeCanceled));
    }

    [Fact]
    public async Task ResetCodeIsTextInHtmlAndPreservedInPlainText()
    {
        var transport = Substitute.For<IAccountEmailTransport>();
        await Create(transport).SendPasswordResetCodeAsync(new ApplicationUser(), "cook@example.test", "<123>&456");
        await transport.Received(1).SendAsync(Arg.Is<AccountEmailMessage>(message =>
            message.Text.Contains("<123>&456", StringComparison.Ordinal) &&
            message.Html.Contains("&lt;123&gt;&amp;456", StringComparison.Ordinal) &&
            !message.Html.Contains("<123>", StringComparison.Ordinal)), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("/relative/link")]
    public async Task InvalidLinkDoesNotReachTransport(string link)
    {
        var transport = Substitute.For<IAccountEmailTransport>();
        await Should.ThrowAsync<ArgumentException>(() => Create(transport).SendConfirmationLinkAsync(new ApplicationUser(), "cook@example.test", link));
        await transport.DidNotReceiveWithAnyArgs().SendAsync(default!, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("request")]
    [InlineData("shutdown")]
    [InlineData("timeout")]
    public async Task SendingHonorsRequestShutdownAndTimeoutCancellation(string cause)
    {
        using var request = new CancellationTokenSource();
        using var shutdown = new CancellationTokenSource();
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(shutdown.Token);
        var transport = Substitute.For<IAccountEmailTransport>();
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        transport.SendAsync(Arg.Any<AccountEmailMessage>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var ct = call.Arg<CancellationToken>();
            started.SetResult(ct);
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        });
        var sender = new IdentityEmailSender(transport,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext { RequestAborted = request.Token } },
            lifetime, Options.Create(new AccountEmailOptions { TimeoutSeconds = 1 }));
        var sending = sender.SendPasswordResetCodeAsync(new ApplicationUser(), "cook@example.test", "123456");
        var token = await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        if (string.Equals(cause, "request", StringComparison.Ordinal))
        {
            await request.CancelAsync();
        }
        else if (string.Equals(cause, "shutdown", StringComparison.Ordinal))
        {
            await shutdown.CancelAsync();
        }
        await Should.ThrowAsync<OperationCanceledException>(() => sending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        token.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task TransportFailureIsNotReportedAsSuccess()
    {
        var transport = Substitute.For<IAccountEmailTransport>();
        var failure = new IOException("Local mail server unavailable");
        transport.SendAsync(Arg.Any<AccountEmailMessage>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(failure));
        (await Should.ThrowAsync<IOException>(() => Create(transport).SendPasswordResetCodeAsync(new ApplicationUser(), "cook@example.test", "123456")))
            .ShouldBeSameAs(failure);
    }

    private static IdentityEmailSender Create(IAccountEmailTransport transport) =>
        new(transport, new HttpContextAccessor(), Substitute.For<IHostApplicationLifetime>(), Options.Create(new AccountEmailOptions()));
}
