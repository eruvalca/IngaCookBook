[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$workflow = Get-Content -LiteralPath (Join-Path (Split-Path $PSScriptRoot) '.github/workflows/deploy.yml') -Raw
$deployment = ($workflow -split '(?m)^  deploy:\r?$', 2)[1]
$steps = @([regex]::Split($deployment, '(?m)^      - ') | Select-Object -Skip 1)
if ($steps.Count -lt 2 -or $steps[0] -notmatch '(?m)^        id: freshness\r?$') {
    throw 'The freshness check must be the first step inside the deployment job.'
}
if ($deployment -notmatch '(?m)^      group: ingacookbook-production\r?$' -or
    $deployment -notmatch '(?m)^      cancel-in-progress: false\r?$') {
    throw 'Production deployment must retain its non-canceling concurrency lock.'
}
foreach ($step in ($steps | Select-Object -Skip 1)) {
    if ($step -notmatch "(?m)^        if: steps\.freshness\.outputs\.deploy == 'true'\r?$") {
        throw 'Every subsequent deployment step must require a successful freshness check.'
    }
}

# Execute the actual inline workflow code, so the regression check cannot drift
# into testing a separate copy of the comparison. Only the GitHub CLI is stubbed.
$run = [regex]::Match($steps[0], '(?ms)^        run: \|\r?\n(?<source>.*)')
if (-not $run.Success) { throw 'The freshness step must contain an inline PowerShell check.' }
$guard = [scriptblock]::Create([regex]::Replace($run.Groups['source'].Value, '(?m)^          ', ''))
$newerSha = 'b' * 40
$olderSha = 'a' * 40
$cases = @(
    @{ Name = 'CurrentCommitDeploys'; Run = $newerSha; Head = $newerSha; ExitCode = 0; Expected = 'deploy=true'; Error = $null },
    @{ Name = 'OlderCommitFinishingLaterIsSkipped'; Run = $olderSha; Head = $newerSha; ExitCode = 0; Expected = 'deploy=false'; Error = $null },
    @{ Name = 'FailedLookupCannotAuthorizeDeployment'; Run = $newerSha; Head = $newerSha; ExitCode = 1; Expected = $null; Error = 'Unable to verify' },
    @{ Name = 'MissingHeadStopsDeployment'; Run = $newerSha; Head = ''; ExitCode = 0; Expected = $null; Error = 'Unable to verify' },
    @{ Name = 'MalformedHeadStopsDeployment'; Run = $newerSha; Head = 'not-a-sha'; ExitCode = 0; Expected = $null; Error = 'Unable to verify' },
    @{ Name = 'LookupExceptionStopsDeployment'; Run = $newerSha; Head = $null; ExitCode = 0; Expected = $null; Error = 'Simulated lookup failure' }
)
$savedEnvironment = @{}
foreach ($name in @('GITHUB_REPOSITORY', 'GITHUB_SHA', 'GITHUB_OUTPUT')) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
}
try {
    foreach ($case in $cases) {
        $outputFile = [IO.Path]::GetTempFileName()
        try {
            $env:GITHUB_REPOSITORY = 'test/notebook'
            $env:GITHUB_SHA = $case.Run
            $env:GITHUB_OUTPUT = $outputFile
            function gh {
                if (($args -join ' ') -cne 'api repos/test/notebook/git/ref/heads/main --jq .object.sha') {
                    throw 'Unexpected GitHub request.'
                }
                Set-Variable -Name LASTEXITCODE -Scope 1 -Value $case.ExitCode
                if ($null -eq $case.Head) { throw 'Simulated lookup failure' }
                $case.Head
            }
            $failure = $null
            try { & $guard }
            catch { $failure = $_.Exception.Message }
            if ($case.Error) {
                if (-not $failure -or -not $failure.Contains($case.Error, [StringComparison]::Ordinal)) {
                    throw "$($case.Name): expected failure was not observed. Actual: $failure"
                }
                if ((Get-Item -LiteralPath $outputFile).Length -ne 0) {
                    throw "$($case.Name): a failed lookup emitted a deployment decision."
                }
            }
            elseif ($failure -or (Get-Content -LiteralPath $outputFile -Raw).Trim() -cne $case.Expected) {
                throw "$($case.Name): incorrect deployment decision. Error: $failure"
            }
            Write-Host "Passed: $($case.Name)"
        }
        finally { Remove-Item -LiteralPath $outputFile }
    }
}
finally {
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name])
    }
}
Write-Host 'Deployment workflow: 6 scenarios passed; lock, first-step ordering, and downstream conditions verified. No external calls made.'
