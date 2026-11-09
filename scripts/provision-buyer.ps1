$ErrorActionPreference = 'Stop'
$backend = Split-Path -Parent $PSScriptRoot
$project = Join-Path $backend 'TAGlobal.Api\TAGlobal.Api.csproj'
$envFile = Join-Path $backend '.env'
if (-not (Test-Path $envFile)) { throw 'ta_global_backend/.env was not found.' }

foreach ($line in Get-Content $envFile) {
    $line = $line.Trim()
    if (-not $line -or $line.StartsWith('#')) { continue }
    $separator = $line.IndexOf('=')
    if ($separator -le 0) { continue }
    $key = $line.Substring(0, $separator).Trim()
    $value = $line.Substring($separator + 1).Trim()
    if ($value.Length -ge 2 -and (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'")))) {
        $value = $value.Substring(1, $value.Length - 2)
    }
    [Environment]::SetEnvironmentVariable($key, $value, 'Process')
}

$name = Read-Host 'Buyer full name'
$username = Read-Host 'Buyer username'
$email = Read-Host 'Buyer email'
$secure = Read-Host 'Buyer password (input is hidden)' -AsSecureString
$password = [System.Net.NetworkCredential]::new('', $secure).Password
if ($password.Length -lt 8) {
    $secure.Dispose()
    throw 'Use a password of at least 8 characters.'
}

$env:TAGLOBAL_BUYER_NAME = $name
$env:TAGLOBAL_BUYER_USERNAME = $username
$env:TAGLOBAL_BUYER_EMAIL = $email
$env:TAGLOBAL_BUYER_PASSWORD = $password
$env:DOTNET_CLI_HOME = Join-Path $backend '.dotnet'
$env:NUGET_PACKAGES = Join-Path $backend '.nuget\packages'
$env:TEMP = Join-Path $backend '.temp'
$env:TMP = $env:TEMP

try {
    $buildOutput = Join-Path (Split-Path -Parent $project) '.temp\buyer-provision-build\'
    dotnet build $project --no-restore -p:UseAppHost=false "-p:OutputPath=$buildOutput"
    if ($LASTEXITCODE -ne 0) { throw 'Buyer provisioning build failed.' }
    $assembly = Join-Path $buildOutput 'TAGlobal.Api.dll'
    dotnet $assembly provision-buyer
    if ($LASTEXITCODE -ne 0) { throw 'Buyer creation failed. Check that PostgreSQL is running and .env connection is correct.' }
}
finally {
    Remove-Item Env:TAGLOBAL_BUYER_NAME, Env:TAGLOBAL_BUYER_USERNAME, Env:TAGLOBAL_BUYER_EMAIL, Env:TAGLOBAL_BUYER_PASSWORD -ErrorAction SilentlyContinue
    $password = $null
    $secure.Dispose()
}
