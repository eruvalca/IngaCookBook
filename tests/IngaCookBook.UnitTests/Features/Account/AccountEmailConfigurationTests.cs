using System.Diagnostics.CodeAnalysis;
using IngaCookBook.Data;
using IngaCookBook.Features.Account.Services.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace IngaCookBook.UnitTests.Features.Account;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class AccountEmailConfigurationTests
{
    // A syntactically valid test key, not a credential for an Azure resource.
    private const string TestConnection = "endpoint=https://example.communication.azure.com/;accesskey=dGVzdA==";

    [Theory]
    [InlineData("None", "Production", typeof(DisabledEmailTransport))]
    [InlineData("None", "Development", typeof(DisabledEmailTransport))]
    [InlineData("Mailpit", "Development", typeof(MailpitEmailTransport))]
    [InlineData("Azure", "Development", typeof(AzureEmailTransport))]
    [InlineData("Azure", "Production", typeof(AzureEmailTransport))]
    public async Task ValidConfigurationStartsAndResolvesTheSelectedSender(string provider, string environment, Type expectedTransport)
    {
        using var host = Create(provider, environment).Build();
        await host.StartAsync(TestContext.Current.CancellationToken);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IEmailSender<ApplicationUser>>().ShouldBeOfType<IdentityEmailSender>();
        scope.ServiceProvider.GetRequiredService<IAccountEmailTransport>().GetType().ShouldBe(expectedTransport);
        var identity = scope.ServiceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value;
        identity.SignIn.RequireConfirmedAccount.ShouldBe(!string.Equals(provider, "None", StringComparison.Ordinal));
        identity.User.RequireUniqueEmail.ShouldBeTrue();
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("Mailpit", "Production", "Email:SenderAddress", "cook@example.test")]
    [InlineData("Unknown", "Development", "Email:SenderAddress", "cook@example.test")]
    [InlineData("Mailpit", "Development", "Email:SenderAddress", "")]
    [InlineData("Mailpit", "Development", "Email:SenderAddress", "Name <cook@example.test>")]
    [InlineData("Mailpit", "Development", "Email:MailpitEndpoint", "http://localhost:1234")]
    [InlineData("Mailpit", "Development", "Email:TimeoutSeconds", "0")]
    [InlineData("Mailpit", "Development", "Email:TimeoutSeconds", "121")]
    [InlineData("Azure", "Development", "ConnectionStrings:communicationemail", "")]
    [InlineData("Azure", "Development", "ConnectionStrings:communicationemail", "secret-invalid-key")]
    public async Task InvalidConfigurationFailsAtStartupWithoutLeakingSecrets(string provider, string environment, string key, string value)
    {
        var builder = Create(provider, environment);
        builder.Configuration[key] = value;
        using var host = builder.Build();
        var error = await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync(TestContext.Current.CancellationToken));
        error.Message.ShouldNotContain("secret-invalid-key");
    }

    [Fact]
    public async Task ProductionDefaultsNeedNoSenderOrCredentialsAndRejectAccidentalEmailSending()
    {
        var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true, EnvironmentName = "Production" });
        AccountEmailConfiguration.Configure(builder);
        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);

        var options = host.Services.GetRequiredService<IOptions<AccountEmailOptions>>().Value;
        options.Enabled.ShouldBeFalse();
        options.SenderAddress.ShouldBeEmpty();
        options.AzureConnectionString.ShouldBeEmpty();
        host.Services.GetRequiredService<IOptions<IdentityOptions>>().Value.SignIn.RequireConfirmedAccount.ShouldBeFalse();
        var transport = host.Services.GetRequiredService<IAccountEmailTransport>();
        await Should.ThrowAsync<InvalidOperationException>(() => transport.SendAsync(
            new("cook@example.test", "subject", "html", "text"), TestContext.Current.CancellationToken));

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private static HostApplicationBuilder Create(string provider, string environment)
    {
        var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true, EnvironmentName = environment });
        builder.Services.AddHttpContextAccessor();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Email:Provider"] = provider,
            ["Email:SenderAddress"] = "cook@example.test",
            ["Email:MailpitEndpoint"] = "smtp://localhost:1234",
            ["ConnectionStrings:communicationemail"] = TestConnection,
        });
        AccountEmailConfiguration.Configure(builder);
        return builder;
    }
}
