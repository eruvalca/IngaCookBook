using Azure.Communication.Email;
using IngaCookBook.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace IngaCookBook.Features.Account.Services.Email;

internal static class AccountEmailConfiguration
{
    public static void Configure(IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<AccountEmailOptions>()
            .Bind(builder.Configuration.GetSection("Email"))
            .Configure(options => options.AzureConnectionString = builder.Configuration.GetConnectionString("communicationemail") ?? "")
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<AccountEmailOptions>, AccountEmailOptionsValidator>();
        builder.Services.AddOptions<IdentityOptions>()
            .Configure<IOptions<AccountEmailOptions>>((identity, email) =>
            {
                identity.SignIn.RequireConfirmedAccount = email.Value.Enabled;
                identity.SignIn.RequireConfirmedEmail = false;
                identity.User.RequireUniqueEmail = true;
            });
        builder.Services.AddScoped<IEmailSender<ApplicationUser>, IdentityEmailSender>();
        builder.Services.AddSingleton<IAccountEmailTransport>(services =>
        {
            var options = services.GetRequiredService<IOptions<AccountEmailOptions>>();
            if (!options.Value.Enabled)
            {
                return new DisabledEmailTransport();
            }
            if (string.Equals(options.Value.Provider, "Mailpit", StringComparison.Ordinal))
            {
                return new MailpitEmailTransport(options);
            }
            return new AzureEmailTransport(new EmailClient(options.Value.AzureConnectionString), options);
        });
    }
}
