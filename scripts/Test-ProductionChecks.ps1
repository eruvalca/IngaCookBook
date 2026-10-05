[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$checks = Join-Path $PSScriptRoot 'Test-Production.ps1'
$cases = @(
    @{ Name = 'ReadyEndpoints'; Responses = @(200); Calls = 7; Sleeps = 0; Error = $null },
    @{ Name = 'IngressTimeoutThenReady'; Responses = @('timeout', 200); Calls = 8; Sleeps = 1; Error = $null },
    @{ Name = 'ConnectionFailureThenReady'; Responses = @('connection', 200); Calls = 8; Sleeps = 1; Error = $null },
    @{ Name = 'TransientGatewayThenReady'; Responses = @(503, 200); Calls = 8; Sleeps = 1; Error = $null },
    @{ Name = 'PersistentTimeoutIsBounded'; Responses = @('timeout'); Calls = 4; Sleeps = 3; Error = 'Simulated timeout' },
    @{ Name = 'PersistentGatewayIsBounded'; Responses = @(502); Calls = 4; Sleeps = 3; Error = '/alive returned HTTP 502' },
    @{ Name = 'MissingRouteFailsImmediately'; Responses = @(404); Calls = 1; Sleeps = 0; Error = '/alive returned HTTP 404' },
    @{ Name = 'UnhealthyPayloadFails'; Responses = @('unhealthy'); Calls = 1; Sleeps = 0; Error = '/alive did not report Healthy' },
    @{ Name = 'UnexpectedFailureIsNotRetried'; Responses = @('unexpected'); Calls = 1; Sleeps = 0; Error = 'Unexpected failure' }
)

foreach ($case in $cases) {
    $state = @{ Calls = 0; Sleeps = 0 }
    function az { '{"properties":{"configuration":{"ingress":{"fqdn":"test.invalid"}}}}' }
    function Invoke-WebRequest {
        param([string] $Uri, [int] $TimeoutSec, [switch] $SkipHttpErrorCheck)
        if (-not $Uri.StartsWith('https://test.invalid/', [StringComparison]::Ordinal) -or
            $TimeoutSec -ne 30 -or -not $SkipHttpErrorCheck) { throw 'Unexpected request contract.' }
        $index = [Math]::Min($state.Calls, $case.Responses.Count - 1)
        $state.Calls++
        $result = $case.Responses[$index]
        switch ($result) {
            'timeout' { throw [System.Threading.Tasks.TaskCanceledException]::new('Simulated timeout') }
            'connection' { throw [System.Net.Http.HttpRequestException]::new('Simulated connection failure') }
            'unexpected' { throw [InvalidOperationException]::new('Unexpected failure') }
            'unhealthy' { return @{ StatusCode = 200; Content = 'Unhealthy' } }
            default { return @{ StatusCode = $result; Content = 'Healthy' } }
        }
    }
    function Start-Sleep {
        param([int] $Seconds)
        if ($Seconds -ne 5) { throw 'Unexpected retry delay.' }
        $state.Sleeps++
    }
    $failure = $null
    try { & $checks -ResourceGroup 'test-only' 6>$null }
    catch { $failure = $_.Exception.Message }
    if ($case.Error) {
        if (-not $failure -or -not $failure.Contains($case.Error, [StringComparison]::Ordinal)) {
            throw "$($case.Name): expected error was not observed. Actual: $failure"
        }
    }
    elseif ($failure) { throw "$($case.Name): $failure" }
    if ($state.Calls -ne $case.Calls -or $state.Sleeps -ne $case.Sleeps) {
        throw "$($case.Name): incorrect request/delay count: $($state.Calls)/$($state.Sleeps)."
    }
    Write-Host "Passed: $($case.Name)"
}
Write-Host 'Production HTTP checks: 9 scenarios passed. No network requests or sleeps performed.'
