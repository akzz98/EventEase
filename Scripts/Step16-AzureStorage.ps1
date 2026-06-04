param(
    [string]$AzureStorageConnectionString = $env:EVENTEASE_AZURE_STORAGE,
    [string]$AzureSqlConnectionString = $env:EVENTEASE_AZURE_SQL,
    [switch]$MigrateFromAzurite
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$toolProject = Join-Path $PSScriptRoot "StorageSetup\StorageSetup.csproj"

if ([string]::IsNullOrWhiteSpace($AzureStorageConnectionString)) {
    Write-Host "Azure Storage connection string required." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Portal checklist:" -ForegroundColor Cyan
    Write-Host "  1. Create Storage account (e.g. cldveventeasestorage) in cldv-eventease-poe"
    Write-Host "  2. Security: enable public blob access if required for image display"
    Write-Host "  3. Access keys -> copy Connection string"
    Write-Host "  4. Set EVENTEASE_AZURE_STORAGE and run this script again"
    Write-Host ""
    Write-Host "App Service (step 17) will use:" -ForegroundColor Cyan
    Write-Host "  AzureStorage__ConnectionString"
    Write-Host "  AzureStorage__ContainerName = venue-images"
    exit 1
}

$env:EVENTEASE_AZURE_STORAGE = $AzureStorageConnectionString
if (-not [string]::IsNullOrWhiteSpace($AzureSqlConnectionString)) {
    $env:EVENTEASE_AZURE_SQL = $AzureSqlConnectionString
}
if ($MigrateFromAzurite) {
    $env:EVENTEASE_MIGRATE_AZURITE = "true"
}

Write-Host "Running Azure Storage setup..." -ForegroundColor Cyan
Push-Location (Join-Path $projectRoot "Scripts\StorageSetup")
try {
    dotnet run --project $toolProject
    if ($LASTEXITCODE -ne 0) { throw "Storage setup tool failed." }
}
finally {
    Pop-Location
    Remove-Item Env:EVENTEASE_MIGRATE_AZURITE -ErrorAction SilentlyContinue
}

Write-Host "Done. Re-upload venue images on the live site if you skipped Azurite migration." -ForegroundColor Green
