using IngaCookBook.Components;
using IngaCookBook.Data;
using IngaCookBook.Features.Account.Endpoints;
using IngaCookBook.Features.Account.Services;
using IngaCookBook.Features.Account.Services.Email;
using IngaCookBook.Features.Installation;
using IngaCookBook.Features.Notebook.Endpoints;
using IngaCookBook.Features.Notebook.Services;
using IngaCookBook.ServiceDefaults;
using IngaCookBook.SharedKernel.Notebook;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.FluentUI.AspNetCore.Components;

var recoveryMode = args.Length > 0 && string.Equals(args[0], "account-recovery", StringComparison.Ordinal);
var builder = WebApplication.CreateBuilder(recoveryMode ? args[1..] : args);
if (recoveryMode)
{
    // Operator output is private terminal output, never telemetry or a web endpoint.
    builder.Logging.ClearProviders();
}

builder.AddServiceDefaults();
builder.AddAzureBlobServiceClient("recipephotos");
builder.Services.AddHttpContextAccessor();
builder.Services.AddOpenApi();
builder.Services.AddScoped<NotebookService>();
builder.Services.AddScoped<INotebookService>(services => services.GetRequiredService<NotebookService>());
builder.Services.AddScoped<IRecipePhotoStore, AzureRecipePhotoStore>();
builder.Services.AddScoped<PhotoCleanupProcessor>();
builder.Services.AddHostedService<PhotoCleanupWorker>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ApplicationRelease>();
builder.Services.AddScoped<IAccountDeletionService, AccountDeletionService>();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization();

builder.Services.AddCascadingAuthenticationState();
// Consumed only by static renderers; interactive components own their lifetimes.
builder.Services.AddCascadingValue("RequestAborted", services =>
    services.GetRequiredService<IHttpContextAccessor>().HttpContext?.RequestAborted ?? CancellationToken.None);
builder.Services.AddFluentUIComponents();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AccountSignInService>();
builder.Services.AddScoped<AccountPasskeyService>();
builder.Services.AddScoped<AccountRegistrationService>();
builder.Services.AddScoped<AccountEmailChangeService>();
builder.Services.AddScoped<AccountTwoFactorService>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("ingacookbookdb")
    ?? throw new InvalidOperationException("Connection string 'ingacookbookdb' not found. Start the application through Aspire or configure ConnectionStrings:ingacookbookdb.");
// The factory supports one context per Blazor operation and also registers the scoped context used by Identity.
builder.Services.AddDbContextFactory<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
builder.EnrichNpgsqlDbContext<ApplicationDbContext>();
// Bound readiness independently of EF's transient retries for normal application operations.
builder.Services.PostConfigure<HealthCheckServiceOptions>(options =>
{
    var databaseCheck = options.Registrations.SingleOrDefault(registration =>
        string.Equals(registration.Name, nameof(ApplicationDbContext), StringComparison.Ordinal));
    if (databaseCheck is not null)
    {
        databaseCheck.Timeout = TimeSpan.FromSeconds(5);
    }
});
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();
AccountRecoveryConfiguration.Configure(builder.Services);

AccountEmailConfiguration.Configure(builder);

await using var app = builder.Build();
if (recoveryMode)
{
    await using var scope = app.Services.CreateAsyncScope();
    Environment.ExitCode = await scope.ServiceProvider.GetRequiredService<AccountRecoveryCommand>()
        .RunAsync(builder.Configuration["user-id"], builder.Configuration["base-url"], Console.Out, Console.Error);
    return;
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseMigrationsEndPoint();
    app.MapOpenApi();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapGet("/app-version", (HttpContext context, ApplicationRelease release) =>
{
    context.Response.Headers.CacheControl = "no-store";
    return TypedResults.Text(release.Id);
}).WithSummary("Check for a new application release");
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(IngaCookBook.UI.UiAssemblyMarker).Assembly);

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();
app.MapNotebookEndpoints();
app.MapDefaultEndpoints();

await app.RunAsync();
