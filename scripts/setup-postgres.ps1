param(
    [Parameter(Mandatory = $true)]
    [string]$AdminUser,
    [Parameter(Mandatory = $true)]
    [string]$AdminPassword,
    [string]$Database = 'taglobal',
    [string]$AppUser = 'taglobal'
)

$securePassword = Read-Host 'New application database password' -AsSecureString
$credential = [System.Net.NetworkCredential]::new('', $securePassword)
$appPassword = $credential.Password.Replace("'", "''")
$env:PGPASSWORD = $AdminPassword
$psql = Join-Path $env:ProgramFiles 'PostgreSQL\18\bin\psql.exe'
if (-not (Test-Path $psql)) { throw "psql was not found at $psql" }

try {
    $createRole = "SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', '$AppUser', '$appPassword') WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = '$AppUser') \gexec"
    & $psql -h localhost -p 5432 -U $AdminUser -d postgres -v ON_ERROR_STOP=1 -c $createRole
    if ($LASTEXITCODE -ne 0) { throw 'Could not create database application role.' }

    $alterRole = "SELECT format('ALTER ROLE %I WITH LOGIN PASSWORD %L', '$AppUser', '$appPassword') \gexec"
    & $psql -h localhost -p 5432 -U $AdminUser -d postgres -v ON_ERROR_STOP=1 -c $alterRole
    if ($LASTEXITCODE -ne 0) { throw 'Could not set database application password.' }

    $createDatabase = "SELECT format('CREATE DATABASE %I OWNER %I', '$Database', '$AppUser') WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = '$Database') \gexec"
    & $psql -h localhost -p 5432 -U $AdminUser -d postgres -v ON_ERROR_STOP=1 -c $createDatabase
    if ($LASTEXITCODE -ne 0) { throw 'Could not create application database.' }
} finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    $appPassword = $null
    $credential = $null
    $securePassword.Dispose()
}

Write-Host "Database '$Database' and role '$AppUser' are ready. Set ConnectionStrings__DefaultConnection before running the API."
