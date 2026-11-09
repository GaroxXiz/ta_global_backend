$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$backendRoot = Join-Path $workspaceRoot 'ta_global_backend'
$apiProject = Join-Path $backendRoot 'TAGlobal.Api\TAGlobal.Api.csproj'
$envFile = Join-Path $backendRoot '.env'
if (-not (Test-Path $envFile)) { throw 'ta_global_backend/.env was not found.' }
foreach ($line in Get-Content $envFile) {
    $line = $line.Trim()
    if (-not $line -or $line.StartsWith('#')) { continue }
    $separator = $line.IndexOf('=')
    if ($separator -le 0) { continue }
    $key = $line.Substring(0, $separator).Trim()
    $value = $line.Substring($separator + 1).Trim()
    [Environment]::SetEnvironmentVariable($key, $value, 'Process')
}
if ($env:Jwt__Secret.Length -lt 32) { throw 'Jwt__Secret in .env must be at least 32 characters.' }
dotnet run --project $apiProject --launch-profile http
