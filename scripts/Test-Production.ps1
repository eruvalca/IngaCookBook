[CmdletBinding()]
param([Parameter(Mandatory)][string] $ResourceGroup)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$app = az containerapp show --resource-group $ResourceGroup --name ingacookbook --output json | ConvertFrom-Json
$origin = 'https://' + $app.properties.configuration.ingress.fqdn
if ($origin -eq 'https://') { throw 'Azure did not return an application hostname.' }

foreach ($path in @('/alive', '/health', '/', '/Account/Login', '/Account/Register', '/manifest.webmanifest', '/app-version')) {
    # A ready first revision can precede public ingress/DNS availability. Retry
    # only safe GETs and transient failures, with a fixed upper bound per path.
    $response = $null
    for ($attempt = 1; $attempt -le 4; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri "$origin$path" -TimeoutSec 30 -SkipHttpErrorCheck
            if ($response.StatusCode -notin @(408, 429, 502, 503, 504) -or $attempt -eq 4) { break }
        }
        catch [System.Net.Http.HttpRequestException], [System.Threading.Tasks.TaskCanceledException] {
            if ($attempt -eq 4) { throw }
        }
        Write-Host "$path is not reachable yet (attempt $attempt/4); retrying in five seconds."
        Start-Sleep -Seconds 5
    }
    if ($response.StatusCode -ne 200) { throw "$path returned HTTP $($response.StatusCode)." }
    if ($path -in @('/alive', '/health') -and $response.Content.Trim() -ne 'Healthy') {
        throw "$path did not report Healthy."
    }
    Write-Host "$path passed."
}
Write-Host "Production HTTP checks passed: $origin"
Write-Host 'Complete the authenticated smoke checks in README.md before inviting the first user.'
