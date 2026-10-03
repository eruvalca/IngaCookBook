using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace IngaCookBook.Testing;

/// <summary>Creates isolated copies of the real AppHost for HTTP and browser tests.</summary>
public static class TestAppHost
{
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
