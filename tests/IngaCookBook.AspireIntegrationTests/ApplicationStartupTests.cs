using System.Diagnostics.CodeAnalysis;
using System.Net;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Azure.Storage.Blobs;
using IngaCookBook.Testing;
using Npgsql;
using Shouldly;
using Xunit;

namespace IngaCookBook.AspireIntegrationTests;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class ApplicationStartupTests(ITestOutputHelper output)
{
    [Fact]
    public async Task AppHostCompletesMigrationsAndServesHealthyApplication()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        await using var builder = await TestAppHost.CreateAsync(timeout.Token);
        builder.Resources.SelectMany(resource => resource.Annotations.OfType<ContainerMountAnnotation>())
            .ShouldBeEmpty();
        await using var app = await builder.BuildAsync(timeout.Token);
        await TestAppHost.StartAsync(app, output.WriteLine, timeout.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("photostorage", timeout.Token);
        await app.ResourceNotifications.WaitForResourceAsync("ingacookbook-migrations", KnownResourceStates.Finished, timeout.Token);

        using var client = app.CreateHttpClient("ingacookbook", "https");
        foreach (var path in new[] { "/health", "/alive", "/", "/Account/Login", "/openapi/v1.json" })
        {
            using var response = await client.GetAsync(new Uri(path, UriKind.Relative), timeout.Token);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, path);
        }

        var home = await client.GetStringAsync(new Uri("/", UriKind.Relative), timeout.Token);
        home.ShouldContain("every batch.");
        home.ShouldContain("fluent-layout");
        using var anonymous = await client.GetAsync(new Uri("/api/notebook/recipes", UriKind.Relative), timeout.Token);
        anonymous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var schema = await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative), timeout.Token);
        schema.ShouldContain("/api/notebook/recipes");
        await VerifyDurablePhotoCleanupAsync(app, timeout.Token);
    }

    private static async Task VerifyDurablePhotoCleanupAsync(DistributedApplication app, CancellationToken ct)
    {
        var blobs = new BlobServiceClient(await app.GetConnectionStringAsync("recipephotos", ct));
        var container = blobs.GetBlobContainerClient("recipe-photos");
        await container.CreateIfNotExistsAsync(cancellationToken: ct);
        var prefix = $"{Guid.NewGuid():N}/";
        var removed = container.GetBlobClient(prefix + "recipe/version/photo");
        var retained = container.GetBlobClient($"{Guid.NewGuid():N}/recipe/version/photo");
        await removed.UploadAsync(BinaryData.FromString("deleted account"), ct);
        await removed.CreateSnapshotAsync(cancellationToken: ct);
        await retained.UploadAsync(BinaryData.FromString("other account"), ct);

        await using var db = new NpgsqlConnection(await app.GetConnectionStringAsync("ingacookbookdb", ct));
        await db.OpenAsync(ct);
        var jobId = Guid.NewGuid();
        await using (var insert = db.CreateCommand())
        {
            insert.CommandText = "INSERT INTO \"PhotoCleanupJobs\" (\"Id\", \"Prefix\", \"QueuedAt\", \"NextAttemptAt\") VALUES ($1, $2, now(), now())";
            insert.Parameters.AddWithValue(jobId);
            insert.Parameters.AddWithValue(prefix);
            (await insert.ExecuteNonQueryAsync(ct)).ShouldBe(1);
        }
        using var cleanupTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cleanupTimeout.CancelAfter(TimeSpan.FromSeconds(90));
        await using var pending = db.CreateCommand();
        pending.CommandText = "SELECT count(*) FROM \"PhotoCleanupJobs\" WHERE \"Id\" = $1";
        pending.Parameters.AddWithValue(jobId);
        using var poll = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        while (Convert.ToInt64(await pending.ExecuteScalarAsync(cleanupTimeout.Token), System.Globalization.CultureInfo.InvariantCulture) != 0)
        {
            await poll.WaitForNextTickAsync(cleanupTimeout.Token);
        }
        (await removed.ExistsAsync(ct)).Value.ShouldBeFalse();
        (await retained.DownloadContentAsync(ct)).Value.Content.ToString().ShouldBe("other account");
    }
}
