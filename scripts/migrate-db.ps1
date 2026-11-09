$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$backend = Join-Path $root 'ta_global_backend'
$api = Join-Path $backend 'TAGlobal.Api\TAGlobal.Api.csproj'
$envFile = Join-Path $backend '.env'
if (-not (Test-Path $envFile)) { throw 'Create ta_global_backend/.env first.' }
foreach ($line in Get-Content $envFile) { $line=$line.Trim(); if(-not $line -or $line.StartsWith('#')){continue}; $i=$line.IndexOf('='); if($i -gt 0){[Environment]::SetEnvironmentVariable($line.Substring(0,$i).Trim(),$line.Substring($i+1).Trim(),'Process')} }
$tool=Join-Path $backend '.dotnet\tools\dotnet-ef.exe'
if(-not(Test-Path $tool)){dotnet tool install dotnet-ef --tool-path (Join-Path $backend '.dotnet\tools') --version 10.0.0}
& $tool database update --project $api --startup-project $api
