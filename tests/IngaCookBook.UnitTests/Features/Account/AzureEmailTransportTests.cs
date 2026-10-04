using System.Diagnostics.CodeAnalysis;
using Azure;
using Azure.Communication.Email;
using IngaCookBook.Features.Account.Services.Email;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace IngaCookBook.UnitTests.Features.Account;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class AzureEmailTransportTests
{
    [Fact]
    public async Task SendsBothBodiesToOnlyTheRequestedRecipientAndWaitsForCompletion()
    {
        var client = Substitute.For<EmailClient>();
        var operation = Substitute.For<EmailSendOperation>();
        operation.Value.Returns(EmailModelFactory.EmailSendResult("operation-id", EmailSendStatus.Succeeded));
        client.SendAsync(Arg.Any<WaitUntil>(), Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>()).Returns(operation);
        using var cancellation = new CancellationTokenSource();
        await Create(client).SendAsync(new("recipient@example.test", "Confirm email", "<p>Confirm</p>", "Confirm"), cancellation.Token);
        await client.Received(1).SendAsync(WaitUntil.Completed, Arg.Is<EmailMessage>(message =>
            message.SenderAddress == "sender@example.test" &&
            message.Recipients.To.Count == 1 && message.Recipients.To[0].Address == "recipient@example.test" &&
            message.Recipients.CC.Count == 0 && message.Recipients.BCC.Count == 0 &&
            message.Content.Subject == "Confirm email" && message.Content.Html == "<p>Confirm</p>" && message.Content.PlainText == "Confirm"), cancellation.Token);
    }

    [Theory]
    [InlineData("Failed")]
    [InlineData("Canceled")]
    [InlineData("Running")]
    public async Task UnsuccessfulOperationIsNotAccepted(string status)
    {
        var client = Substitute.For<EmailClient>();
        var operation = Substitute.For<EmailSendOperation>();
        operation.Value.Returns(EmailModelFactory.EmailSendResult("operation-id", new EmailSendStatus(status)));
        client.SendAsync(Arg.Any<WaitUntil>(), Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>()).Returns(operation);
        await Should.ThrowAsync<InvalidOperationException>(() => Create(client).SendAsync(Message(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SdkFailurePropagates()
    {
        var client = Substitute.For<EmailClient>();
        var failure = new RequestFailedException(429, "Throttled");
        client.SendAsync(Arg.Any<WaitUntil>(), Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<EmailSendOperation>(failure));
        (await Should.ThrowAsync<RequestFailedException>(() => Create(client).SendAsync(Message(), TestContext.Current.CancellationToken)))
            .ShouldBeSameAs(failure);
    }

    [Fact]
    public async Task SdkCancellationPropagates()
    {
        var client = Substitute.For<EmailClient>();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        client.SendAsync(Arg.Any<WaitUntil>(), Arg.Any<EmailMessage>(), cancellation.Token)
            .Returns(Task.FromCanceled<EmailSendOperation>(cancellation.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => Create(client).SendAsync(Message(), cancellation.Token));
    }

    private static AccountEmailMessage Message() => new("recipient@example.test", "subject", "<p>body</p>", "body");
    private static AzureEmailTransport Create(EmailClient client) =>
        new(client, Options.Create(new AccountEmailOptions { SenderAddress = "sender@example.test" }));
}
