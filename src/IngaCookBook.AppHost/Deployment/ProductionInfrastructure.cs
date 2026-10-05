using Aspire.Hosting.Azure;
using Azure.Provisioning.AppContainers;
using Azure.Provisioning.Expressions;
using Azure.Provisioning.KeyVault;
using Azure.Provisioning.OperationalInsights;
using Azure.Provisioning.PostgreSql;
using Azure.Provisioning.Storage;

namespace IngaCookBook.AppHost.Deployment;

internal static class ProductionInfrastructure
{
    internal static IResourceBuilder<IResourceWithConnectionString> Add(IDistributedApplicationBuilder builder)
    {
        // The pinned ACA integration provisions a Basic container registry by default.
        // Test-DeploymentModel verifies that default along with the explicit sizing below.
        builder.AddAzureContainerAppEnvironment("cookbook")
            .WithDashboard()
            .ConfigureInfrastructure(infrastructure =>
            {
                var resources = infrastructure.GetProvisionableResources();
                var logs = resources.OfType<OperationalInsightsWorkspace>().Single();
                logs.RetentionInDays = 30;
                logs.WorkspaceCapping.DailyQuotaInGB = 0.1;
            });

        // Keep the database password stable across stateless CI runs. Aspire stores the
        // connection string in Key Vault and grants the workloads access using identities.
        var password = builder.AddParameter("postgres-password", secret: true);
        var userName = builder.AddParameter("postgres-user", "cookbookadmin", publishValueAsDefault: true);
        return builder.AddAzurePostgresFlexibleServer("postgres")
            .WithPasswordAuthentication(userName: userName, password: password)
            .ConfigureInfrastructure(infrastructure =>
            {
                var server = infrastructure.GetProvisionableResources().OfType<PostgreSqlFlexibleServer>().Single();
                server.Sku = new PostgreSqlFlexibleServerSku
                {
                    Name = "Standard_B1ms",
                    Tier = PostgreSqlFlexibleServerSkuTier.Burstable,
                };
                server.Storage.StorageSizeInGB = 32;
                server.Backup.BackupRetentionDays = 7;
                server.Backup.GeoRedundantBackup = PostgreSqlFlexibleServerGeoRedundantBackupEnum.Disabled;
                server.HighAvailability.Mode = PostgreSqlFlexibleServerHighAvailabilityMode.Disabled;
                // Both the web app and EF bundle consume these Key Vault connection
                // secrets. Require TLS/hostname verification instead of Npgsql's Prefer
                // default, and avoid loading unused Kerberos libraries in Linux images.
                foreach (var secret in infrastructure.GetProvisionableResources().OfType<KeyVaultSecret>())
                {
                    secret.Properties.Value = BicepFunction.Interpolate($"{secret.Properties.Value};SSL Mode=VerifyFull;GSS Encryption Mode=Disable");
                }
            })
            .AddDatabase("ingacookbookdb", "ingacookbook");
    }

    internal static void ConfigurePhotos(IResourceBuilder<AzureStorageResource> storage)
    {
        storage.ConfigureInfrastructure(infrastructure =>
        {
            var account = infrastructure.GetProvisionableResources().OfType<StorageAccount>().Single();
            account.Sku.Name = StorageSkuName.StandardLrs;
            account.AllowBlobPublicAccess = false;
            account.AllowSharedKeyAccess = false;
            account.IsHnsEnabled = false;
            infrastructure.Add(new BlobService("photoBlobs")
            {
                Parent = account,
                DeleteRetentionPolicy = new DeleteRetentionPolicy { IsEnabled = true, Days = 7 },
                ContainerDeleteRetentionPolicy = new DeleteRetentionPolicy { IsEnabled = true, Days = 7 },
            });
        });
    }

    internal static void ConfigureWeb(IResourceBuilder<ProjectResource> web)
    {
        web.WithEnvironment("ASPNETCORE_ENVIRONMENT", "Production")
            .WithEnvironment("HealthChecks__Enabled", "true")
            // ACA terminates TLS; the container is reachable only through its managed ingress.
            .WithEnvironment("ASPNETCORE_FORWARDEDHEADERS_ENABLED", "true")
            .PublishAsAzureContainerApp((_, app) =>
            {
                app.Configuration.ActiveRevisionsMode = ContainerAppActiveRevisionsMode.Single;
                app.Configuration.Ingress.AllowInsecure = false;
                app.Configuration.Ingress.StickySessionsAffinity = StickySessionAffinity.Sticky;
                app.Template.Scale.MinReplicas = 1;
                app.Template.Scale.MaxReplicas = 1;
                var container = app.Template.Containers[0].Value!;
                container.Resources.Cpu = 0.5;
                container.Resources.Memory = "1Gi";
                container.Probes.Add(new ContainerAppProbe
                {
                    ProbeType = ContainerAppProbeType.Readiness,
                    HttpGet = new ContainerAppHttpRequestInfo
                    {
                        Path = "/health",
                        Port = app.Configuration.Ingress.TargetPort,
                        // Probes reach the HTTP container directly, bypassing TLS ingress.
                        HttpHeaders = { new ContainerAppHttpHeaderInfo { Name = "X-Forwarded-Proto", Value = "https" } },
                    },
                    PeriodSeconds = 10,
                    TimeoutSeconds = 6,
                    FailureThreshold = 3,
                });
                container.Probes.Add(new ContainerAppProbe
                {
                    ProbeType = ContainerAppProbeType.Liveness,
                    HttpGet = new ContainerAppHttpRequestInfo
                    {
                        Path = "/alive",
                        Port = app.Configuration.Ingress.TargetPort,
                        HttpHeaders = { new ContainerAppHttpHeaderInfo { Name = "X-Forwarded-Proto", Value = "https" } },
                    },
                    InitialDelaySeconds = 30,
                    PeriodSeconds = 30,
                    TimeoutSeconds = 5,
                    FailureThreshold = 3,
                });
            });
    }
}
