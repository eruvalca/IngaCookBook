var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithRepl();
var database = postgres.AddDatabase("ingacookbookdb", "ingacookbook");
var blobs = builder.AddAzureStorage("photostorage")
    .RunAsEmulator(emulator => emulator.WithDataVolume())
    .AddBlobs("recipephotos");

var web = builder.AddProject<Projects.IngaCookBook>("ingacookbook")
    .WithReference(database)
    .WithReference(blobs)
    .WaitFor(blobs)
    .WaitFor(database)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

// Pin the managed EF tool to the application's EF Core version rather than the global tool.
#pragma warning disable ASPIREDOTNETTOOL // The agreed Aspire EF migration integration uses the experimental tool resource API.
var migrations = web.AddEFMigrations("ingacookbook-migrations", "IngaCookBook.Data.ApplicationDbContext",
        tool => tool.WithToolVersion("10.0.12"))
    .WithReference(database)
    .WithReference(blobs)
    .WaitFor(database)
    // EF constructs the web host at design time but does not serve HTTP. Avoid inherited DCP endpoint tokens.
    .WithEnvironment("ASPNETCORE_URLS", "http://127.0.0.1:0")
    .WithMigrationOutputDirectory("Data/Migrations")
    .WithMigrationNamespace("IngaCookBook.Migrations");
#pragma warning restore ASPIREDOTNETTOOL

if (builder.ExecutionContext.IsRunMode)
{
    migrations.RunDatabaseUpdateOnStart();
    web.WaitForCompletion(migrations);
    postgres.WithPgAdmin(pgAdmin => pgAdmin.WithExplicitStart());
}

// The host owns process shutdown (Ctrl+C/signals); there is no caller request lifetime.
await builder.Build().RunAsync(CancellationToken.None);
