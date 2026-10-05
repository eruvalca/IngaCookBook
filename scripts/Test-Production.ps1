[CmdletBinding()]
param([Parameter(Mandatory)][string] $ResourceGroup)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$app = az containerapp show --resource-group $ResourceGroup --name ingacookbook --output json | ConvertFrom-Json
$origin = 'https://' + $app.properties.configuration.ingress.fqdn
if ($origin -eq 'https://') { throw 'Azure did not return an application hostname.' }

foreach ($path in @('/alive', '/health', '/', '/Account/Login', '/Account/Register', '/manifest.webmanifest', '/app-version')) {
    $response = Invoke-WebRequest -Uri "$origin$path" -TimeoutSec 30 -SkipHttpErrorCheck
    if ($response.StatusCode -ne 200) { throw "$path returned HTTP $($response.StatusCode)." }
    if ($path -in @('/alive', '/health') -and $response.Content.Trim() -ne 'Healthy') {
        throw "$path did not report Healthy."
    }
}
Write-Host "Production HTTP checks passed: $origin"
Write-Host 'Complete the authenticated smoke checks in README.md before inviting the first user.'
