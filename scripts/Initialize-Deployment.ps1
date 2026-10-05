[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory)][guid] $SubscriptionId,
    [Parameter(Mandatory)][mailaddress] $AlertEmail,
    [ValidatePattern('^[\w.-]+/[\w.-]+$')][string] $Repository = 'eruvalca/IngaCookBook',
    [ValidateSet('centralus')][string] $Location = 'centralus',
    [ValidateSet('rg-ingacookbook-prod')][string] $ResourceGroup = 'rg-ingacookbook-prod'
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
Set-StrictMode -Version Latest

# This script bootstraps credentials and policy only. Aspire owns all application infrastructure.
# -WhatIf exits before calling any CLI, reading credentials, or writing remote state.
if (-not $PSCmdlet.ShouldProcess("$SubscriptionId/$ResourceGroup and $Repository",
    'Register Azure providers; create the resource group, GitHub OIDC identity, scoped roles, $75 budget and protected production environment; enable deployment')) {
    return
}

Get-Command az, gh -ErrorAction Stop | Out-Null
$account = az account show --subscription $SubscriptionId --output json | ConvertFrom-Json
if ($account.state -ne 'Enabled') { throw 'The selected Azure subscription is not enabled.' }
$repo = gh repo view $Repository --json viewerPermission | ConvertFrom-Json
if ($repo.viewerPermission -ne 'ADMIN') { throw 'GitHub repository admin access is required for the production environment.' }
$oidc = gh api "repos/$Repository/actions/oidc/customization/sub" | ConvertFrom-Json
if (-not $oidc.use_default -or [string]::IsNullOrWhiteSpace($oidc.sub_claim_prefix)) {
    throw 'Expected GitHub default OIDC claims with a reported subject prefix. Review custom claims before provisioning.'
}
# GitHub's default prefix can include immutable owner/repository IDs. Use the
# authoritative prefix instead of reconstructing the obsolete name-only form.
$subject = "$($oidc.sub_claim_prefix):environment:production"

$environmentRoute = "repos/$Repository/environments/production"
$environments = gh api "repos/$Repository/environments" --paginate --jq '.environments[].name'
if ('production' -in $environments) {
    $environment = gh api $environmentRoute | ConvertFrom-Json
    if (-not $environment.deployment_branch_policy.custom_branch_policies) {
        throw 'Existing production environment must restrict deployments to the main branch. Review it before rerunning.'
    }
    $policies = gh api "$environmentRoute/deployment-branch-policies" | ConvertFrom-Json
    if (@($policies.branch_policies).Count -ne 1 -or $policies.branch_policies[0].name -ne 'main' -or $policies.branch_policies[0].type -ne 'branch') {
        throw 'Existing production environment has a different branch policy. Preserve and review it before rerunning.'
    }
}
else {
    @{ deployment_branch_policy = @{ protected_branches = $false; custom_branch_policies = $true } } |
        ConvertTo-Json -Depth 5 | gh api --method PUT $environmentRoute --input - | Out-Null
    @{ name = 'main'; type = 'branch' } | ConvertTo-Json |
        gh api --method POST "$environmentRoute/deployment-branch-policies" --input - | Out-Null
}

foreach ($provider in @('Microsoft.App', 'Microsoft.DBforPostgreSQL', 'Microsoft.Storage',
        'Microsoft.ContainerRegistry', 'Microsoft.OperationalInsights', 'Microsoft.KeyVault',
        'Microsoft.ManagedIdentity', 'Microsoft.Consumption')) {
    az provider register --namespace $provider --subscription $SubscriptionId --wait --only-show-errors --output none
}

az group create --subscription $SubscriptionId --name $ResourceGroup --location $Location --output none
$scope = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup"
$identityName = 'ingacookbook-github'
$identity = az identity create --subscription $SubscriptionId --resource-group $ResourceGroup --name $identityName --location $Location --output json | ConvertFrom-Json
az identity federated-credential create --subscription $SubscriptionId --resource-group $ResourceGroup `
    --identity-name $identityName --name github-production --issuer https://token.actions.githubusercontent.com `
    --subject $subject --audiences api://AzureADTokenExchange --output none

# Contributor provisions resources; RBAC Administrator lets Aspire assign workload identities.
# Both are confined to this application's RG, not the subscription.
$roles = @('b24988ac-6180-42a0-ab88-20f7382dd24c', 'f58310d9-a9f6-439a-9e8d-f62e7b41a168')
$existingRoles = az role assignment list --subscription $SubscriptionId --scope $scope --output json | ConvertFrom-Json
foreach ($role in $roles) {
    if (-not ($existingRoles | Where-Object { $_.principalId -eq $identity.principalId -and $_.roleDefinitionId.EndsWith($role, [StringComparison]::OrdinalIgnoreCase) })) {
        az role assignment create --subscription $SubscriptionId --scope $scope --role $role `
            --assignee-object-id $identity.principalId --assignee-principal-type ServicePrincipal --output none
    }
}

$budgetUrl = "https://management.azure.com$scope/providers/Microsoft.Consumption/budgets"
$budgets = az rest --method get --url "${budgetUrl}?api-version=2024-08-01" --output json | ConvertFrom-Json
$existingBudget = $budgets.value | Where-Object name -EQ 'ingacookbook-monthly'
$startDate = [datetime]::UtcNow.ToString('yyyy-MM-01T00:00:00Z', [cultureinfo]::InvariantCulture)
$notifications = @{}
foreach ($threshold in @(50, 80, 100)) {
    $notifications["actual-$threshold"] = @{
        enabled = $true; operator = 'GreaterThanOrEqualTo'; threshold = $threshold
        thresholdType = 'Actual'; contactEmails = @($AlertEmail.Address); locale = 'en-us'
    }
}
$budget = @{ properties = @{
    amount = 75; category = 'Cost'; timeGrain = 'Monthly'
    timePeriod = @{ startDate = $startDate }; notifications = $notifications
} }
if ($existingBudget) {
    $budget.eTag = $existingBudget.eTag
    $budget.properties.timePeriod = $existingBudget.properties.timePeriod
}
$budgetFile = [IO.Path]::GetTempFileName()
try {
    $budget | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $budgetFile -Encoding utf8NoBOM
    az rest --method put --url "$budgetUrl/ingacookbook-monthly?api-version=2024-08-01" --body "@$budgetFile" --output none
}
finally { Remove-Item -LiteralPath $budgetFile -ErrorAction SilentlyContinue }

$variables = @{
    AZURE_SUBSCRIPTION_ID = $SubscriptionId.ToString(); AZURE_TENANT_ID = $account.tenantId
    AZURE_CLIENT_ID = $identity.clientId; AZURE_LOCATION = $Location; AZURE_RESOURCE_GROUP = $ResourceGroup
}
foreach ($name in $variables.Keys) {
    gh variable set $name --repo $Repository --env production --body $variables[$name]
}
$secrets = gh secret list --repo $Repository --env production --json name | ConvertFrom-Json
if ('POSTGRES_PASSWORD' -notin @($secrets | ForEach-Object name)) {
    $databases = az resource list --subscription $SubscriptionId --resource-group $ResourceGroup `
        --resource-type Microsoft.DBforPostgreSQL/flexibleServers --output json | ConvertFrom-Json
    if (@($databases).Count -gt 0) {
        throw 'A database already exists but POSTGRES_PASSWORD is missing. Restore its current password to the GitHub secret; do not regenerate it.'
    }
    # Standard base64 is safe in an Npgsql connection string; a fixed prefix ensures
    # Azure's upper/lower/digit requirements, with 256 bits of random entropy.
    $password = 'Ic9!' + [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    try { $password | gh secret set POSTGRES_PASSWORD --repo $Repository --env production }
    finally { $password = $null }
}

# A repository variable gates the entire deployment job before environment access.
gh variable set DEPLOYMENT_ENABLED --repo $Repository --body true
Write-Host 'Bootstrap complete. Production is restricted to main, with OIDC access and monthly budget notifications.'
Write-Host 'No application resources have been deployed. Commit/push the reviewed workflow or manually dispatch it to deploy.'
