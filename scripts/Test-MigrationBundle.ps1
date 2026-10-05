[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$repoRoot = Split-Path $PSScriptRoot
$artifacts = Join-Path $repoRoot 'TestResults/deployment-model'
if (-not (Test-Path -LiteralPath (Join-Path $artifacts 'efmigrations/ingacookbook-migrations'))) {
    throw 'Run Test-DeploymentModel.ps1 first to generate the real Linux migration bundle.'
}
$suffix = [guid]::NewGuid().ToString('N')
$network = "inga-migration-test-$suffix"
$database = "inga-migration-db-$suffix"
$image = "inga-migration-test:$suffix"
$password = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
$networkCreated = $false
$databaseCreated = $false
$imageCreated = $false
try {
    docker build --quiet --file (Join-Path $artifacts 'ingacookbook-migrations.Dockerfile') --tag $image (Join-Path $artifacts 'efmigrations') | Out-Null
    $imageCreated = $true
    docker network create $network | Out-Null
    $networkCreated = $true
    docker run --detach --name $database --network $network --network-alias database `
        --env POSTGRES_DB=ingacookbook --env POSTGRES_USER=cookbookadmin --env "POSTGRES_PASSWORD=$password" `
        --health-cmd 'pg_isready -U cookbookadmin -d ingacookbook' --health-interval 1s --health-timeout 3s --health-retries 60 postgres:16 | Out-Null
    $databaseCreated = $true
    $deadline = [datetime]::UtcNow.AddMinutes(2)
    do {
        $health = docker inspect --format '{{.State.Health.Status}}' $database
        if ($health -eq 'healthy') { break }
        if ([datetime]::UtcNow -gt $deadline -or $health -eq 'unhealthy') { throw 'Disposable PostgreSQL did not become healthy.' }
        Start-Sleep -Seconds 1
    } while ($true)

    $expectedMigrations = @(Get-ChildItem (Join-Path $repoRoot 'src/IngaCookBook/Data/Migrations') -Filter '*.cs' |
        Where-Object { $_.Name -match '^\d+_.+(?<!\.Designer)\.cs$' }).Count
    foreach ($attempt in 1..2) {
        # No host ports or development resources. The second run proves repeatability.
        docker run --rm --network $network --env "ConnectionStrings__ingacookbookdb=Host=database;Database=ingacookbook;Username=cookbookadmin;Password=$password;GSS Encryption Mode=Disable" `
            --env ConnectionStrings__recipephotos=https://design-time.invalid --env Email__Provider=None $image |
            Set-Content -LiteralPath (Join-Path $artifacts "bundle-run-$attempt.log")
        $count = docker exec $database psql -U cookbookadmin -d ingacookbook -tAc 'SELECT count(*) FROM "__EFMigrationsHistory"'
        if ([int]$count -ne $expectedMigrations -or $expectedMigrations -eq 0) { throw 'The bundle did not apply all repository migrations.' }
    }
    $passkeys = docker exec $database psql -U cookbookadmin -d ingacookbook -tAc "SELECT count(*) FROM information_schema.tables WHERE table_name = 'AspNetUserPasskeys'"
    if ([int]$passkeys -ne 1) { throw 'The deployed model is missing Identity schema version 3 passkeys.' }
    Write-Host "Migration bundle: applied $expectedMigrations migrations to PostgreSQL 16, repeated successfully, and preserved the Identity passkey schema."
}
finally {
    # These exact random names belong to this invocation; never remove development resources.
    if ($databaseCreated) { docker rm --force --volumes $database | Out-Null }
    if ($networkCreated) { docker network rm $network | Out-Null }
    if ($imageCreated) { docker image rm $image | Out-Null }
    $password = $null
}
