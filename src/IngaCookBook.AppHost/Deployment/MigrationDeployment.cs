using Aspire.Hosting.Azure;
using Aspire.Hosting.EntityFrameworkCore;
using Aspire.Hosting.Pipelines;
using Azure.Provisioning.AppContainers;
using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using Microsoft.Extensions.DependencyInjection;

namespace IngaCookBook.AppHost.Deployment;

// The pinned Aspire EF integration and custom deployment pipeline expose preview APIs.
#pragma warning disable ASPIREDOTNETTOOL, ASPIREPIPELINES001
internal static class MigrationDeployment
{
    internal const string JobName = "ingacookbook-migrations";
    internal const string RunStepName = "run-database-migrations";

    internal static void Configure(IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> web, IResourceBuilder<EFMigrationResource> migrations)
    {
        var gateConfigured = false;
        builder.Pipeline.AddStep(new PipelineStep
        {
            Name = "validate-migration-gate",
            Description = "Verify the resolved Azure pipeline gates web provisioning on migration success.",
            RequiredBySteps = [WellKnownPipelineSteps.Publish, WellKnownPipelineSteps.DeployPrereq],
            Action = _ =>
            {
                if (!gateConfigured)
                {
                    throw new InvalidOperationException("Migration gate was not configured. Refusing to publish or deploy.");
                }
                return Task.CompletedTask;
            },
        });
        migrations.PublishAsMigrationBundle(publishContainer: true, baseImage: "mcr.microsoft.com/dotnet/aspnet:10.0")
            .PublishAsAzureContainerAppJob((_, job) =>
            {
                job.Name = JobName;
                job.Configuration.TriggerType = ContainerAppJobTriggerType.Manual;
                job.Configuration.ReplicaTimeout = 600;
                job.Configuration.ReplicaRetryLimit = 0;
                job.Configuration.ManualTriggerConfig.Parallelism = 1;
                job.Configuration.ManualTriggerConfig.ReplicaCompletionCount = 1;
                job.Template.Containers[0].Value!.Resources.Cpu = 0.5;
                job.Template.Containers[0].Value!.Resources.Memory = "1Gi";
            })
            .WithPipelineStepFactory(_ => new PipelineStep
            {
                Name = RunStepName,
                Description = "Run the migration job and require successful completion before releasing the web app.",
                RequiredBySteps = [WellKnownPipelineSteps.Deploy],
                Action = async context =>
                {
                    var subscription = builder.Configuration["Azure:SubscriptionId"]
                        ?? throw new InvalidOperationException("Azure:SubscriptionId is required for deployment.");
                    var group = builder.Configuration["Azure:ResourceGroup"]
                        ?? throw new InvalidOperationException("Azure:ResourceGroup is required for deployment.");
                    var credentials = context.Services.GetRequiredService<ITokenCredentialProvider>().TokenCredential;
                    // Retrying a POST after losing its response could start a second job.
                    // The runner retries only reads of the execution returned by this start.
                    var options = new ArmClientOptions();
                    options.Retry.MaxRetries = 0;
                    var client = new ArmClient(credentials, subscription, options);
                    var job = client.GetContainerAppJobResource(ContainerAppJobResource.CreateResourceIdentifier(subscription, group, JobName));
                    await MigrationJobRunner.RunAsync(job, TimeProvider.System, context.CancellationToken).ConfigureAwait(false);
                },
            })
            .WithPipelineConfiguration(context =>
            {
                var jobTarget = migrations.Resource.GetDeploymentTargetAnnotation()?.DeploymentTarget;
                var webTarget = web.Resource.GetDeploymentTargetAnnotation()?.DeploymentTarget;
                // Aspire resolves a preliminary graph before BeforeStart materializes ACA
                // targets, then resolves the full graph again before publish/deploy.
                if (jobTarget is null && webTarget is null)
                {
                    gateConfigured = false;
                    return;
                }

                if (jobTarget is null || webTarget is null)
                {
                    throw new InvalidOperationException("Both migration and web deployment targets are required.");
                }
                var run = context.Steps.Single(step => string.Equals(step.Name, RunStepName, StringComparison.Ordinal));
                OrderSteps(run, context.GetSteps(jobTarget, WellKnownPipelineTags.ProvisionInfrastructure),
                    context.GetSteps(webTarget, WellKnownPipelineTags.ProvisionInfrastructure));
                gateConfigured = true;
            });
    }

    internal static void OrderSteps(PipelineStep run, IEnumerable<PipelineStep> jobProvisionSteps,
        IEnumerable<PipelineStep> webProvisionSteps)
    {
        var jobs = jobProvisionSteps.ToArray();
        var apps = webProvisionSteps.ToArray();
        if (jobs.Length == 0 || apps.Length == 0)
        {
            throw new InvalidOperationException("Cannot deploy without both migration and web provisioning steps.");
        }

        // Gate the actual ARM write, not the deploy-summary step that runs after it.
        foreach (var job in jobs)
        {
            run.DependsOn(job);
        }
        foreach (var app in apps)
        {
            app.DependsOn(run);
        }
    }
}
#pragma warning restore ASPIREDOTNETTOOL, ASPIREPIPELINES001
