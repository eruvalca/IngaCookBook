using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace IngaCookBook.Testing;

/// <summary>Creates isolated copies of the real AppHost for HTTP and browser tests.</summary>
public static class TestAppHost
{
    public static async Task StartAsync(DistributedApplication app, Action<string> writeOutput, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(writeOutput);
        try
        {
            await app.StartAsync(cancellationToken);
            await app.ResourceNotifications.WaitForResourceHealthyAsync("ingacookbook", cancellationToken);
        }
        catch
        {
            // Capture startup failures before disposal removes the test resources.
            // Diagnostics have their own bound and must not replace the original error.
            try
            {
                using var diagnostics = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var logs = app.Services.GetRequiredService<ResourceLoggerService>();
                foreach (var name in new[] { "ingacookbook-migrations", "ingacookbook", "postgres", "photostorage" })
                {
                    if (app.ResourceNotifications.TryGetCurrentState(name, out var state))
                    {
                        writeOutput($"Startup resource {name}: {state.Snapshot.State?.Text}");
                    }
                    await foreach (var batch in logs.GetAllAsync(name).WithCancellation(diagnostics.Token))
                    {
                        foreach (var line in batch.TakeLast(40)) { writeOutput($"[{name}] {line.Content}"); }
                    }
                }
            }
            catch (Exception diagnosticError) when (diagnosticError is OperationCanceledException or IOException or InvalidOperationException)
            {
                writeOutput($"Startup diagnostics could not complete: {diagnosticError.Message}");
            }
            throw;
        }
    }

    public static async Task<IDistributedApplicationTestingBuilder> CreateAsync(CancellationToken cancellationToken)
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IngaCookBook_AppHost>(
            ["--environment=Development"], cancellationToken);
        try
        {
            // Never attach tests to development data. Preserve the real resource graph,
            // health checks and migration dependencies, but use disposable storage.
            foreach (var resource in builder.Resources)
            {
                foreach (var mount in resource.Annotations.OfType<ContainerMountAnnotation>().ToArray())
                {
                    resource.Annotations.Remove(mount);
                }
            }
            foreach (var container in builder.Resources.OfType<ContainerResource>())
            {
                builder.CreateResourceBuilder(container).WithLifetime(ContainerLifetime.Session);
            }

            return builder;
        }
        catch
        {
            await builder.DisposeAsync();
            throw;
        }
    }
}
