using Azure;
using Azure.ResourceManager.AppContainers;

namespace IngaCookBook.AppHost.Deployment;

internal static class MigrationJobRunner
{
    internal static async Task RunAsync(ContainerAppJobResource job, TimeProvider clock, CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(12), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            // ARM operation completion means "started", not "migration succeeded".
            var started = await job.StartAsync(WaitUntil.Completed, cancellationToken: linked.Token).ConfigureAwait(false);
            var execution = started.Value.Name;
            if (string.IsNullOrWhiteSpace(execution))
            {
                throw new InvalidOperationException("Azure did not return the migration execution name. Deployment stopped.");
            }

            while (true)
            {
                Response<ContainerAppJobExecutionResource> response;
                try
                {
                    response = await job.GetContainerAppJobExecutionAsync(execution, linked.Token).ConfigureAwait(false);
                }
                catch (RequestFailedException exception) when (exception.Status is 404 or 408 or 429 or >= 500)
                {
                    // A new execution can take time to appear. Retry this read only;
                    // the shared deadline also bounds throttling and service outages.
                    await Task.Delay(TimeSpan.FromSeconds(5), clock, linked.Token).ConfigureAwait(false);
                    continue;
                }
                var status = response.Value.Data.Status?.ToString();
                switch (status)
                {
                    case "Succeeded":
                        return;
                    case "Running":
                    case "Pending":
                    case "Processing":
                        await Task.Delay(TimeSpan.FromSeconds(5), clock, linked.Token).ConfigureAwait(false);
                        break;
                    default:
                        throw new InvalidOperationException($"Migration execution {execution} has status '{status ?? "unknown"}'. Deployment stopped; inspect the job logs.");
                }
            }
        }
        catch (OperationCanceledException exception) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Migration execution did not succeed within 12 minutes. Deployment stopped; the Azure job may still be running.", exception);
        }
    }
}
