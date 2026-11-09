param([string]$Username='superadmin_taglobal',[string]$Email='superadmin@taglobal.com')
$ErrorActionPreference='Stop'
$backend=Split-Path -Parent $PSScriptRoot
$project=Join-Path $backend 'TAGlobal.Api\TAGlobal.Api.csproj'
foreach($line in Get-Content (Join-Path $backend '.env')){$line=$line.Trim();if(-not $line-or$line.StartsWith('#')){continue};$i=$line.IndexOf('=');if($i -gt 0){[Environment]::SetEnvironmentVariable($line.Substring(0,$i).Trim(),$line.Substring($i+1).Trim(),'Process')}}
$secure=Read-Host 'Superadmin password (input is hidden)' -AsSecureString
$password=[System.Net.NetworkCredential]::new('', $secure).Password
if($password.Length -lt 12){$secure.Dispose();throw 'Use a password of at least 12 characters.'}
$env:TAGLOBAL_ADMIN_USERNAME=$Username;$env:TAGLOBAL_ADMIN_EMAIL=$Email;$env:TAGLOBAL_ADMIN_PASSWORD=$password
$env:DOTNET_CLI_HOME=Join-Path $backend '.dotnet';$env:NUGET_PACKAGES=Join-Path $backend '.nuget\packages';$env:TEMP=Join-Path $backend '.temp';$env:TMP=$env:TEMP
try{dotnet run --project $project --no-build -- provision-admin;if($LASTEXITCODE -ne 0){throw 'Provisioning failed. Check that PostgreSQL is running and .env connection is correct.'}}
finally{Remove-Item Env:TAGLOBAL_ADMIN_USERNAME,Env:TAGLOBAL_ADMIN_EMAIL,Env:TAGLOBAL_ADMIN_PASSWORD -ErrorAction SilentlyContinue;$password=$null;$secure.Dispose()}
