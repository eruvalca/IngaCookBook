using System.Security.Claims;
using IngaCookBook.Data;
using IngaCookBook.Features.Account.Services;
using IngaCookBook.Features.Notebook.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace IngaCookBook.IntegrationTests;

internal sealed class NotebookTestStore(PostgreSqlContainer container, ServiceProvider provider) : IAsyncDisposable
{
    internal IDbContextFactory<ApplicationDbContext> Factory { get; } = provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
    internal MemoryPhotos Photos { get; } = new();
    internal AsyncServiceScope CreateScope() => provider.CreateAsyncScope();

    internal static async Task<NotebookTestStore> CreateAsync(CancellationToken ct, IInterceptor? interceptor = null)
    {
        var container = new PostgreSqlBuilder("postgres:18.3").Build();
        ServiceProvider? provider = null;
        try
        {
            await container.StartAsync(ct);
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContextFactory<ApplicationDbContext>(options =>
            {
                options.UseNpgsql(container.GetConnectionString(), postgres => postgres.EnableRetryOnFailure());
                if (interceptor is not null)
                {
                    options.AddInterceptors(interceptor);
                }
            });
            services.AddIdentityCore<ApplicationUser>(options => options.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
                .AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddScoped<IAccountDeletionService, AccountDeletionService>();
            provider = services.BuildServiceProvider();
            var store = new NotebookTestStore(container, provider);
            await using var db = await store.Factory.CreateDbContextAsync(ct);
            await db.Database.MigrateAsync(ct);
            return store;
        }
        catch
        {
            if (provider is not null)
            {
                await provider.DisposeAsync();
            }
            await container.DisposeAsync();
            throw;
        }
    }

    internal async Task<NotebookService> OwnerAsync(string id, CancellationToken ct)
    {
        await using var db = await Factory.CreateDbContextAsync(ct);
        db.Users.Add(new ApplicationUser { Id = id, UserName = id });
        await db.SaveChangesAsync(ct);
        return Service(id);
    }

    internal NotebookService Service(string id)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)], "test"));
        return new(Factory, new FixedHttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } },
            new UserState(principal), Photos, NullLogger<NotebookService>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await provider.DisposeAsync();
        await container.DisposeAsync();
    }

    private sealed class FixedHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    private sealed class UserState(ClaimsPrincipal principal) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(principal));
    }

    internal sealed class MemoryPhotos : IRecipePhotoStore
    {
        internal Dictionary<string, byte[]> Items { get; } = new(StringComparer.Ordinal);
        internal Func<Task>? AfterSave { get; set; }
        internal Exception? SaveFailure { get; set; }
        internal Exception? DeleteFailure { get; set; }
        internal Exception? CleanupFailure { get; set; }
        public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
        {
            if (SaveFailure is not null) { throw SaveFailure; }
            await using var bytes = new MemoryStream();
            await content.CopyToAsync(bytes, cancellationToken);
            Items.Add(key, bytes.ToArray());
            if (AfterSave is not null)
            {
                await AfterSave();
            }
        }
        public Task<Stream> OpenAsync(string key, CancellationToken cancellationToken) => Task.FromResult<Stream>(new MemoryStream(Items[key]));
        public Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            if (DeleteFailure is not null) { throw DeleteFailure; }
            Items.Remove(key);
            return Task.CompletedTask;
        }
        public Task DeletePrefixAsync(string prefix, CancellationToken cancellationToken)
        {
            if (CleanupFailure is not null) { throw CleanupFailure; }
            foreach (var key in Items.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
            {
                Items.Remove(key);
            }
            return Task.CompletedTask;
        }
    }
}
