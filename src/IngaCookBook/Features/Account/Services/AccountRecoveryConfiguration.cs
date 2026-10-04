using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

namespace IngaCookBook.Features.Account.Services;

internal static class AccountRecoveryConfiguration
{
    internal static void Configure(IServiceCollection services)
    {
        services.AddScoped<AccountRecoveryCommand>();
        // Web and operator processes must use the same application name and persisted key ring.
        services.AddDataProtection().SetApplicationName("IngaCookBook");
        services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(1));
    }
}
