using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using IngaCookBook.Data;
using IngaCookBook.Features.Account.Services;
using IngaCookBook.Features.Notebook.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace IngaCookBook.IntegrationTests;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class AccountRecoveryTests
{
    [Fact]
    public async Task OwnerResetIsBoundToAccountSingleUseAndPreservesWorkspaceAndTwoFactor()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        await using var postgres = new PostgreSqlBuilder("postgres:18.3").Build();
        await postgres.StartAsync(timeout.Token);
        var services = CreateServices(postgres.GetConnectionString());
        await using var provider = services.BuildServiceProvider();
        string userId;
        Guid workspaceId;
        string token;

        await using (var scope = provider.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await database.Database.MigrateAsync(timeout.Token);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = "cook@example.test", Email = "cook@example.test" };
            (await users.CreateAsync(user, "Initial-Kitchen-123!")).Succeeded.ShouldBeTrue();
            (await users.SetTwoFactorEnabledAsync(user, true)).Succeeded.ShouldBeTrue();
            userId = user.Id;
            workspaceId = Guid.NewGuid();
            database.Set<WorkspaceEntity>().Add(new() { Id = workspaceId, OwnerId = user.Id, Name = "My kitchen" });
            await database.SaveChangesAsync(timeout.Token);

            using var output = new StringWriter(CultureInfo.InvariantCulture);
            using var error = new StringWriter(CultureInfo.InvariantCulture);
            (await scope.ServiceProvider.GetRequiredService<AccountRecoveryCommand>()
                .RunAsync(user.Id, "https://recipes.example/", output, error)).ShouldBe(0);
            error.ToString().ShouldBeEmpty();
            var link = new Uri(output.ToString().Split(Environment.NewLine).Single(line => line.StartsWith("https://", StringComparison.Ordinal)));
            link.AbsolutePath.ShouldBe("/Account/ResetPassword");
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(QueryHelpers.ParseQuery(link.Query)["code"].ToString()));
            provider.GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>().Value.TokenLifespan.ShouldBe(TimeSpan.FromHours(1));
        }

        // A fresh context proves the link works for the persisted account, not only tracked state.
        await using var verification = provider.CreateAsyncScope();
        var persistedUsers = verification.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var persisted = await persistedUsers.FindByIdAsync(userId);
        persisted.ShouldNotBeNull();
        var other = new ApplicationUser { UserName = "other@example.test", Email = "other@example.test" };
        (await persistedUsers.CreateAsync(other, "Other-Kitchen-123!")).Succeeded.ShouldBeTrue();
        (await persistedUsers.ResetPasswordAsync(other, token, "Stolen-Kitchen-456!")).Succeeded.ShouldBeFalse();
        (await persistedUsers.CheckPasswordAsync(other, "Other-Kitchen-123!")).ShouldBeTrue();

        (await persistedUsers.ResetPasswordAsync(persisted, token, "Recovered-Kitchen-456!")).Succeeded.ShouldBeTrue();
        (await persistedUsers.CheckPasswordAsync(persisted, "Initial-Kitchen-123!")).ShouldBeFalse();
        (await persistedUsers.CheckPasswordAsync(persisted, "Recovered-Kitchen-456!")).ShouldBeTrue();
        (await persistedUsers.ResetPasswordAsync(persisted, token, "Replayed-Kitchen-789!")).Succeeded.ShouldBeFalse();
        persisted.EmailConfirmed.ShouldBeFalse();
        persisted.TwoFactorEnabled.ShouldBeTrue();
        var workspace = await verification.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Set<WorkspaceEntity>().AsNoTracking().SingleAsync(timeout.Token);
        workspace.Id.ShouldBe(workspaceId);
        workspace.OwnerId.ShouldBe(userId);
        workspace.Name.ShouldBe("My kitchen");
    }

    [Fact]
    public async Task ExpiredRecoveryTokenCannotChangePassword()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        await using var postgres = new PostgreSqlBuilder("postgres:18.3").Build();
        await postgres.StartAsync(timeout.Token);
        var services = CreateServices(postgres.GetConnectionString());
        // Expire immediately without sleeping or changing a system-wide clock.
        services.PostConfigure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromSeconds(-1));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync(timeout.Token);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = "cook@example.test", Email = "cook@example.test" };
        (await users.CreateAsync(user, "Initial-Kitchen-123!")).Succeeded.ShouldBeTrue();
        var token = await users.GeneratePasswordResetTokenAsync(user);

        (await users.ResetPasswordAsync(user, token, "Expired-Kitchen-456!")).Succeeded.ShouldBeFalse();
        (await users.CheckPasswordAsync(user, "Initial-Kitchen-123!")).ShouldBeTrue();
    }

    private static ServiceCollection CreateServices(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
        services.AddIdentityCore<ApplicationUser>(options => options.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
            .AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
        AccountRecoveryConfiguration.Configure(services);
        // Tests must never read or write a developer's persistent key ring.
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        return services;
    }
}
