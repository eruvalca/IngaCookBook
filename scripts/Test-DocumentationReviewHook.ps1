#requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot
$hookScript = Join-Path $PSScriptRoot 'Invoke-DocumentationReviewHook.ps1'
$fixtureRoot = Join-Path $repoRoot ('artifacts/documentation-hook-tests/' + [guid]::NewGuid().ToString('N') + '/workspace with spaces')
New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
$passed = 0

function Invoke-FixtureGit([string[]] $Arguments) {
    $output = & git -C $fixtureRoot @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Fixture Git command failed: $($output -join "`n")"
    }
}

function Set-FixtureFile([string] $Path, [string] $Content) {
    $fullPath = Join-Path $fixtureRoot $Path
    New-Item -ItemType Directory -Path (Split-Path $fullPath) -Force | Out-Null
    [IO.File]::WriteAllText($fullPath, $Content)
}

function Invoke-Hook([string] $EventName, [string] $Turn, [hashtable] $Extra = @{}) {
    $payload = @{
        hook_event_name = $EventName
        session_id = 'fixture-session'
        turn_id = $Turn
        cwd = $fixtureRoot
        permission_mode = 'default'
    }
    foreach ($key in $Extra.Keys) {
        $payload[$key] = $Extra[$key]
    }
    $output = $payload | ConvertTo-Json -Compress | & pwsh -NoProfile -NonInteractive -File $hookScript
    if ($LASTEXITCODE -ne 0) {
        throw "Hook exited $LASTEXITCODE."
    }
    return ($output -join "`n") | ConvertFrom-Json -AsHashtable
}

function Assert-Result([string] $Name, [bool] $Condition) {
    if (-not $Condition) {
        throw "FAIL: $Name"
    }
    $script:passed++
    Write-Host "PASS: $Name"
}

function Invoke-WindowsManifestHook($Handler, [hashtable] $Payload, [string] $Shell) {
    $json = $Payload | ConvertTo-Json -Compress
    $output = if ($Shell -eq 'cmd') {
        $json | & $env:ComSpec /d /s /c $Handler.commandWindows
    }
    else {
        $json | & $Shell -NoProfile -NonInteractive -Command $Handler.commandWindows
    }
    return @{ ExitCode = $LASTEXITCODE; Output = $output -join "`n" }
}

# All mutations and commits below belong to this isolated, ignored test repo.
Invoke-FixtureGit @('init', '--quiet')
Invoke-FixtureGit @('config', 'user.name', 'Hook fixture')
Invoke-FixtureGit @('config', 'user.email', 'hook-fixture@example.invalid')
Invoke-FixtureGit @('config', 'commit.gpgsign', 'false')
Invoke-FixtureGit @('config', 'core.hooksPath', '.disabled-hooks')
Set-FixtureFile '.gitignore' "artifacts/`n"
Set-FixtureFile 'README.md' "# Fixture`n"

$result = Invoke-Hook 'UserPromptSubmit' 'unborn'
Assert-Result 'Initial repository receives pre-commit review instructions' (
    $result.hookSpecificOutput.additionalContext -match 'Before any authorized commit')
Assert-Result 'Unchanged untracked files do not trigger a finishing pass' (
    (Invoke-Hook 'Stop' 'unborn').Count -eq 0)
Set-FixtureFile 'new file.md' "# New document`n"
Assert-Result 'New untracked file triggers a review' (
    (Invoke-Hook 'Stop' 'unborn').decision -eq 'block')
Assert-Result 'Repeated Stop does not request another pass' (
    (Invoke-Hook 'Stop' 'unborn').Count -eq 0)

Invoke-FixtureGit @('add', '--all')
Invoke-FixtureGit @('commit', '--quiet', '-m', 'Fixture baseline')
Invoke-Hook 'UserPromptSubmit' 'clean' | Out-Null
Assert-Result 'Read-only turn in a clean repository stays quiet' (
    (Invoke-Hook 'Stop' 'clean').Count -eq 0)

Set-FixtureFile 'README.md' "# Existing dirty edit`n"
Invoke-Hook 'UserPromptSubmit' 'dirty' | Out-Null
Assert-Result 'Pre-existing dirty work alone stays quiet' (
    (Invoke-Hook 'Stop' 'dirty').Count -eq 0)
Set-FixtureFile 'README.md' "# Changed again in the turn`n"
$result = Invoke-Hook 'Stop' 'dirty'
Assert-Result 'Further edits to the same dirty file trigger review' (
    $result.decision -eq 'block' -and $result.reason -match 'README.md')

Invoke-Hook 'UserPromptSubmit' 'staging' | Out-Null
Invoke-FixtureGit @('add', 'README.md')
Assert-Result 'Index-only change is detected' (
    (Invoke-Hook 'Stop' 'staging').decision -eq 'block')
Invoke-Hook 'UserPromptSubmit' 'partial' | Out-Null
Set-FixtureFile 'README.md' "# Unstaged portion of an already-staged file`n"
Assert-Result 'Partial staging does not hide worktree edits' (
    (Invoke-Hook 'Stop' 'partial').decision -eq 'block')

Invoke-Hook 'UserPromptSubmit' 'commit' | Out-Null
Invoke-FixtureGit @('add', 'README.md')
Invoke-FixtureGit @('commit', '--quiet', '-m', 'Fixture change')
Assert-Result 'A commit during the turn still triggers review' (
    (Invoke-Hook 'Stop' 'commit').decision -eq 'block')

Invoke-Hook 'UserPromptSubmit' 'delete' | Out-Null
Remove-Item -LiteralPath (Join-Path $fixtureRoot 'new file.md')
Assert-Result 'Deleted files trigger review' (
    (Invoke-Hook 'Stop' 'delete').decision -eq 'block')

Invoke-Hook 'UserPromptSubmit' 'guard' | Out-Null
Set-FixtureFile 'source.cs' 'class Fixture;'
Assert-Result 'Codex stop_hook_active guard prevents continuation loops' (
    (Invoke-Hook 'Stop' 'guard' @{ stop_hook_active = $true }).Count -eq 0)
Assert-Result 'A different session cannot consume the baseline' (
    (Invoke-Hook 'Stop' 'guard' @{ session_id = 'other-session' }).Count -eq 0)
Assert-Result 'A missing baseline stays quiet' (
    (Invoke-Hook 'Stop' 'missing').Count -eq 0)
Assert-Result 'Plan-mode prompt does not set up a finishing pass' (
    (Invoke-Hook 'UserPromptSubmit' 'plan' @{ permission_mode = 'plan' }).Count -eq 0)
Assert-Result 'Plan-mode Stop never continues editing' (
    (Invoke-Hook 'Stop' 'guard' @{ permission_mode = 'plan' }).Count -eq 0)

# A duplicate prompt event must not overwrite the original comparison baseline.
Invoke-Hook 'UserPromptSubmit' 'duplicate' | Out-Null
Set-FixtureFile 'source.cs' 'class ChangedFixture;'
Invoke-Hook 'UserPromptSubmit' 'duplicate' | Out-Null
Assert-Result 'Duplicate start event preserves the original baseline' (
    (Invoke-Hook 'Stop' 'duplicate').decision -eq 'block')

# Replay the duplicate-review wording and the explicit final-response contract.
# A documentation edit by itself must still not count as a completed review.
$reports = @(
    @{ Name = 'canonical'; Text = 'Documentation review: complete — setup guidance updated.'; Reported = $true }
    @{ Name = 'unchanged-docs'; Text = 'Documentation review: complete — no updates needed.'; Reported = $true }
    @{ Name = 'markdown'; Text = '- **Documentation review:** complete; relevant guidance is current.'; Reported = $true }
    @{ Name = 'original-final'; Text = 'The detailed investigation report includes reproduction details, evidence, and upgrade criteria. Documentation review is complete; formatting and diff checks passed.'; Reported = $true }
    @{ Name = 'original-continuation'; Text = 'Documentation review was already completed. No further edits were needed.'; Reported = $true }
    @{ Name = 'blocked'; Text = 'Documentation review: blocked — the required documentation is unavailable.'; Reported = $true }
    @{ Name = 'missing'; Text = $null; Reported = $false }
    @{ Name = 'empty'; Text = ''; Reported = $false }
    @{ Name = 'unrelated'; Text = 'Code review is complete. Tests passed.'; Reported = $false }
    @{ Name = 'docs-edit-only'; Text = 'Updated README.md.'; Reported = $false }
    @{ Name = 'planned'; Text = 'I will complete the documentation review next.'; Reported = $false }
    @{ Name = 'negated'; Text = 'Documentation review is not complete.'; Reported = $false }
    @{ Name = 'pending'; Text = 'Documentation review: pending.'; Reported = $false }
    @{ Name = 'quoted'; Text = '> Documentation review: complete.'; Reported = $false }
    @{ Name = 'inline-example'; Text = 'Use `Documentation review: complete.` after reviewing.'; Reported = $false }
    @{ Name = 'fenced'; Text = "``````text`nDocumentation review: complete.`n``````"; Reported = $false }
    @{ Name = 'unclosed-fence'; Text = "~~~text`nDocumentation review: complete."; Reported = $false }
    @{ Name = 'after-fence'; Text = "~~~text`nDocumentation review: pending.`n~~~`nDocumentation review: complete."; Reported = $true }
)
foreach ($report in $reports) {
    $turn = 'report-' + $report.Name
    Invoke-Hook 'UserPromptSubmit' $turn | Out-Null
    Set-FixtureFile 'README.md' $turn
    $result = Invoke-Hook 'Stop' $turn @{ last_assistant_message = $report.Text }
    Assert-Result "Final report '$($report.Name)' has the expected reminder behavior" (
        $(if ($report.Reported) { $result.Count -eq 0 } else { $result.decision -eq 'block' }))
}

Invoke-Hook 'UserPromptSubmit' 'ack-snapshot' | Out-Null
Set-FixtureFile 'source.cs' 'class ReviewedFixture;'
$acknowledgement = @{ last_assistant_message = 'Documentation review: complete — no updates needed.' }
Assert-Result 'Explicit acknowledgement avoids the first finishing prompt' (
    (Invoke-Hook 'Stop' 'ack-snapshot' $acknowledgement).Count -eq 0)
Assert-Result 'An acknowledged snapshot stays quiet if Stop is repeated without message text' (
    (Invoke-Hook 'Stop' 'ack-snapshot').Count -eq 0)
Set-FixtureFile 'source.cs' 'class EditedAfterReviewFixture;'
Assert-Result 'Further edits are not covered by a stale acknowledgement' (
    (Invoke-Hook 'Stop' 'ack-snapshot' $acknowledgement).decision -eq 'block')
Assert-Result 'A stale acknowledgement still requests at most one fallback pass' (
    (Invoke-Hook 'Stop' 'ack-snapshot' $acknowledgement).Count -eq 0)

Invoke-Hook 'UserPromptSubmit' 'ack-new-turn' | Out-Null
Set-FixtureFile 'source.cs' 'class NewTurnFixture;'
Assert-Result 'Acknowledgements do not leak into a different turn' (
    (Invoke-Hook 'Stop' 'ack-new-turn').decision -eq 'block')

$stateFiles = Get-ChildItem -LiteralPath (Join-Path $fixtureRoot 'artifacts/agent-hooks/documentation') -Filter '*.json'
$reportedStates = @($stateFiles | ForEach-Object {
    Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json -AsHashtable
} | Where-Object { $_.reportedFingerprint })
Assert-Result 'Acknowledgement state stores hashes without message text' (
    $reportedStates.Count -gt 0 -and
    @($reportedStates | Where-Object {
        $_.reportedFingerprint -notmatch '^[A-F0-9]{64}$' -or
        $_.reportedMessageHash -notmatch '^[A-F0-9]{64}$' -or
        ($_ | ConvertTo-Json) -match 'Documentation review'
    }).Count -eq 0)

$invalidJson = '{invalid' | & pwsh -NoProfile -NonInteractive -File $hookScript
Assert-Result 'Malformed input fails open with a JSON warning' (
    $LASTEXITCODE -eq 0 -and ($invalidJson | ConvertFrom-Json -AsHashtable).systemMessage)
$result = Invoke-Hook 'UserPromptSubmit' 'no-repo' @{ cwd = (Join-Path $fixtureRoot 'does-not-exist') }
Assert-Result 'Missing repository warns without dropping the early reminder' (
    $result.systemMessage -and $result.hookSpecificOutput.additionalContext)

# Exercise manifest commands through both Windows shell families. A cmd-only
# check misses PowerShell expansion of $variables inside nested quoted commands.
$manifest = Get-Content -LiteralPath (Join-Path $repoRoot '.codex/hooks.json') -Raw | ConvertFrom-Json
$startHandler = $manifest.hooks.UserPromptSubmit[0].hooks[0]
$stopHandler = $manifest.hooks.Stop[0].hooks[0]
Assert-Result 'Manifest registers only the two approved documentation handlers' (
    @($manifest.hooks.PSObject.Properties).Count -eq 2 -and
    @($manifest.hooks.UserPromptSubmit).Count -eq 1 -and
    @($manifest.hooks.UserPromptSubmit[0].hooks).Count -eq 1 -and
    @($manifest.hooks.Stop).Count -eq 1 -and
    @($manifest.hooks.Stop[0].hooks).Count -eq 1 -and
    $startHandler.type -eq 'command' -and $stopHandler.type -eq 'command' -and
    $startHandler.command -match 'Invoke-DocumentationReviewHook.ps1' -and
    $startHandler.commandWindows -match 'Invoke-DocumentationReviewHook.ps1' -and
    $stopHandler.command -eq $startHandler.command -and
    $stopHandler.commandWindows -eq $startHandler.commandWindows)

if ($IsWindows) {
    $scriptDirectory = Join-Path $fixtureRoot 'scripts'
    New-Item -ItemType Directory -Path $scriptDirectory -Force | Out-Null
    Copy-Item -LiteralPath $hookScript -Destination $scriptDirectory
    Push-Location $scriptDirectory
    try {
        foreach ($shell in @('pwsh', 'powershell', 'cmd')) {
            $payload = @{
                hook_event_name = 'UserPromptSubmit'
                session_id = 'launcher'
                turn_id = $shell
                cwd = $scriptDirectory
            }
            $execution = Invoke-WindowsManifestHook $startHandler $payload $shell
            $result = $execution.Output | ConvertFrom-Json -AsHashtable
            Assert-Result "$shell prompt launcher resolves paths with spaces and preserves stdin" (
                $execution.ExitCode -eq 0 -and $result.hookSpecificOutput.additionalContext -and -not $result.systemMessage)

            $payload.hook_event_name = 'Stop'
            $execution = Invoke-WindowsManifestHook $stopHandler $payload $shell
            Assert-Result "$shell Stop launcher stays quiet for an unchanged workspace" (
                $execution.ExitCode -eq 0 -and ($execution.Output | ConvertFrom-Json -AsHashtable).Count -eq 0)
            Set-FixtureFile "launcher-$shell.md" 'A change after the manifest prompt hook.'
            $execution = Invoke-WindowsManifestHook $stopHandler $payload $shell
            Assert-Result "$shell Stop launcher requests a review after an edit" (
                $execution.ExitCode -eq 0 -and ($execution.Output | ConvertFrom-Json -AsHashtable).decision -eq 'block')
            $execution = Invoke-WindowsManifestHook $stopHandler $payload $shell
            Assert-Result "$shell Stop launcher does not repeat the finishing pass" (
                $execution.ExitCode -eq 0 -and ($execution.Output | ConvertFrom-Json -AsHashtable).Count -eq 0)

            $payload.turn_id = "$shell-acknowledged"
            $payload.hook_event_name = 'UserPromptSubmit'
            $execution = Invoke-WindowsManifestHook $startHandler $payload $shell
            Set-FixtureFile "launcher-$shell.md" 'Reviewed change after the prompt hook.'
            $payload.hook_event_name = 'Stop'
            $payload.last_assistant_message = 'Documentation review: complete — hook guidance updated.'
            $execution = Invoke-WindowsManifestHook $stopHandler $payload $shell
            Assert-Result "$shell Stop launcher accepts a completed review without a continuation" (
                $execution.ExitCode -eq 0 -and ($execution.Output | ConvertFrom-Json -AsHashtable).Count -eq 0)
        }
    }
    finally {
        Pop-Location
    }
}

Assert-Result 'Hook state is ignored by Git' (
    -not ((& git -C $fixtureRoot status --porcelain --untracked-files=all) -match 'artifacts/'))
Write-Host "Documentation hook checks: $passed passed, 0 failed, 0 skipped."
Write-Host "Fixture: $fixtureRoot"
