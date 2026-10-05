[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
Set-StrictMode -Version Latest
$repoRoot = Split-Path $PSScriptRoot
$output = Join-Path $repoRoot 'TestResults/deployment-model'
$saved = @{}
$inputs = @{
    Azure__SubscriptionId = '00000000-0000-0000-0000-000000000000'
    Azure__ResourceGroup = 'rg-ingacookbook-validation'; Azure__Location = 'centralus'
    Parameters__postgres_password = 'ValidationOnly-NotAProductionSecret-9!'
}
Push-Location $repoRoot
try {
    foreach ($key in $inputs.Keys) {
        $saved[$key] = [Environment]::GetEnvironmentVariable($key)
        [Environment]::SetEnvironmentVariable($key, $inputs[$key])
    }
    # Separate state from Production; publish generates files and never provisions Azure.
    aspire publish --environment Validation -o $output --non-interactive
    # Aspire publishes compute modules separately from main.bicep because the
    # native deployment pipeline owns image building and migration ordering.
    foreach ($bicep in Get-ChildItem -LiteralPath $output -Filter '*.bicep' -Recurse) {
        az bicep build --file $bicep.FullName --only-show-errors
    }
}
finally {
    foreach ($key in $saved.Keys) { [Environment]::SetEnvironmentVariable($key, $saved[$key]) }
    Pop-Location
}

$template = Get-Content -Raw -LiteralPath (Join-Path $output 'main.json') | ConvertFrom-Json -Depth 100
function Get-ModuleResource([string] $Module, [string] $ResourceType) {
    $moduleTemplate = Get-Content -Raw -LiteralPath (Join-Path $output "$Module/$Module.json") | ConvertFrom-Json -Depth 100
    $resources = @($moduleTemplate.resources | Where-Object type -EQ $ResourceType)
    if ($resources.Count -ne 1) { throw "Expected exactly one $ResourceType in $Module" }
    return $resources[0]
}
$script:checks = 0
function Assert-Deployment([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
    $script:checks++
}

$web = Get-ModuleResource 'ingacookbook' 'Microsoft.App/containerApps'
$container = $web.properties.template.containers[0]
$config = $web.properties.configuration
Assert-Deployment ($web.properties.template.scale.minReplicas -eq 1 -and $web.properties.template.scale.maxReplicas -eq 1) 'Web must remain at one replica.'
Assert-Deployment ($container.resources.cpu -eq "[json('0.5')]" -and $container.resources.memory -eq '1Gi') 'Web sizing changed.'
Assert-Deployment ($config.activeRevisionsMode -eq 'Single' -and $config.ingress.stickySessions.affinity -eq 'sticky') 'Blazor requires the selected single-revision/sticky-session configuration.'
Assert-Deployment ($config.ingress.external -eq $true -and $config.ingress.allowInsecure -eq $false) 'Public HTTPS ingress is required.'
Assert-Deployment ($config.runtime.dotnet.autoConfigureDataProtection -eq $true) 'Shared Data Protection must be enabled.'
foreach ($pair in @(@('Email__Provider', 'None'), @('ASPNETCORE_ENVIRONMENT', 'Production'),
        @('HealthChecks__Enabled', 'true'), @('ASPNETCORE_FORWARDEDHEADERS_ENABLED', 'true'))) {
    $value = @($container.env | Where-Object name -EQ $pair[0])[0].value
    Assert-Deployment ($value -eq $pair[1]) "Incorrect deployment setting: $($pair[0])"
}
$readiness = @($container.probes | Where-Object type -EQ 'Readiness')[0]
$liveness = @($container.probes | Where-Object type -EQ 'Liveness')[0]
Assert-Deployment ($readiness.httpGet.path -eq '/health' -and $liveness.httpGet.path -eq '/alive') 'Readiness and liveness must use separate HTTP probes.'
foreach ($probe in @($readiness, $liveness)) {
    Assert-Deployment (@($probe.httpGet.httpHeaders | Where-Object { $_.name -eq 'X-Forwarded-Proto' -and $_.value -eq 'https' }).Count -eq 1) 'HTTP probes must bypass HTTPS redirects through the trusted proxy header.'
}

$job = Get-ModuleResource 'ingacookbook-migrations' 'Microsoft.App/jobs'
Assert-Deployment ($job.properties.configuration.triggerType -eq 'Manual') 'Migrations must run as an explicit job.'
Assert-Deployment ($job.properties.configuration.replicaRetryLimit -eq 0 -and $job.properties.configuration.replicaTimeout -eq 600) 'Migration retry/timeout policy changed.'
Assert-Deployment ($job.properties.configuration.manualTriggerConfig.parallelism -eq 1 -and $job.properties.configuration.manualTriggerConfig.replicaCompletionCount -eq 1) 'Only one migration replica may run.'
$database = Get-ModuleResource 'postgres' 'Microsoft.DBforPostgreSQL/flexibleServers'
Assert-Deployment ($database.sku.name -eq 'Standard_B1ms' -and $database.properties.storage.storageSizeGB -eq 32) 'Database sizing changed.'
Assert-Deployment ($database.properties.backup.backupRetentionDays -eq 7) 'Database backup retention changed.'
$postgresTemplate = Get-Content -Raw -LiteralPath (Join-Path $output 'postgres/postgres.json') | ConvertFrom-Json -Depth 100
$connectionSecrets = @($postgresTemplate.resources | Where-Object type -EQ 'Microsoft.KeyVault/vaults/secrets')
Assert-Deployment ($connectionSecrets.Count -eq 2) 'Expected server and database connection secrets.'
foreach ($secret in $connectionSecrets) {
    Assert-Deployment ($secret.properties.value.Contains(';SSL Mode=VerifyFull;GSS Encryption Mode=Disable', [StringComparison]::Ordinal)) 'Production database connections must verify TLS and disable unused Kerberos.'
}
Assert-Deployment ($template.parameters.postgres_user.defaultValue -eq 'cookbookadmin') 'The database administrator name must remain stable between deployments.'
$storage = Get-ModuleResource 'photostorage' 'Microsoft.Storage/storageAccounts'
Assert-Deployment ($storage.sku.name -eq 'Standard_LRS' -and -not $storage.properties.allowBlobPublicAccess -and -not $storage.properties.allowSharedKeyAccess) 'Photos require LRS storage and identity-based private blob access.'
$blobs = Get-ModuleResource 'photostorage' 'Microsoft.Storage/storageAccounts/blobServices'
Assert-Deployment ($blobs.properties.deleteRetentionPolicy.days -eq 7 -and $blobs.properties.containerDeleteRetentionPolicy.days -eq 7) 'Photo recovery retention changed.'
$registry = Get-ModuleResource 'cookbook-acr' 'Microsoft.ContainerRegistry/registries'
Assert-Deployment ($registry.sku.name -eq 'Basic') 'Registry must use the Basic tier.'
$logs = Get-ModuleResource 'cookbook' 'Microsoft.OperationalInsights/workspaces'
Assert-Deployment ($logs.properties.retentionInDays -eq 30 -and $logs.properties.workspaceCapping.dailyQuotaGb -eq "[json('0.1')]") 'Log retention or ingestion cap changed.'
$dashboard = Get-ModuleResource 'cookbook' 'Microsoft.App/managedEnvironments/dotNetComponents'
Assert-Deployment ($dashboard.properties.componentType -eq 'AspireDashboard') 'Managed Aspire dashboard is missing.'
Assert-Deployment (-not ($template.resources.name -match 'mailpit|pgadmin|azurite')) 'Local developer services must not be deployed.'
$dockerfile = Get-Content -Raw -LiteralPath (Join-Path $output 'ingacookbook-migrations.Dockerfile')
Assert-Deployment ($dockerfile.Contains('FROM mcr.microsoft.com/dotnet/aspnet:10.0', [StringComparison]::Ordinal)) 'Migration bundle needs the ASP.NET Core runtime.'
Assert-Deployment ((Get-Item -LiteralPath (Join-Path $output 'efmigrations/ingacookbook-migrations')).Length -gt 0) 'The Linux migration bundle was not generated.'
Write-Host "Deployment model: $script:checks checks passed; Bicep compiled; Linux migration bundle generated. No Azure resources created."
