param(
    [string]$AppName = "cldv-eventease-app",
    [string]$ResourceGroup = "cldv-eventease-poe",
    [string]$PlanName = "cldv-eventease-plan",
    [string]$Location = "southafricanorth",
    [string]$AzureSqlConnection = $env:EVENTEASE_AZURE_SQL,
    [string]$AzureStorageConnection = $env:EVENTEASE_AZURE_STORAGE,
    [string]$ZipPath = ""
)

$ErrorActionPreference = "Stop"
$az = "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd"
$projectRoot = Split-Path -Parent $PSScriptRoot

if (-not (Test-Path $az)) {
    $az = "az"
}

if ([string]::IsNullOrWhiteSpace($ZipPath)) {
    $ZipPath = Join-Path $projectRoot "EventEase-deploy.zip"
}

if ([string]::IsNullOrWhiteSpace($AzureSqlConnection) -or [string]::IsNullOrWhiteSpace($AzureStorageConnection)) {
    Write-Host "Set EVENTEASE_AZURE_SQL and EVENTEASE_AZURE_STORAGE before running." -ForegroundColor Yellow
    exit 1
}

if (-not (Test-Path $ZipPath)) {
    Write-Host "Missing $ZipPath — run .\Scripts\Step17-AppService.ps1 first." -ForegroundColor Yellow
    exit 1
}

& $az account show *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Run 'az login' first." -ForegroundColor Yellow
    exit 1
}

$appExists = $false
try {
    & $az webapp show --name $AppName --resource-group $ResourceGroup -o none 2>$null
    if ($LASTEXITCODE -eq 0) { $appExists = $true }
}
catch { }

if (-not $appExists) {
    Write-Host "Creating App Service plan and web app..." -ForegroundColor Cyan
    & $az appservice plan create `
        --name $PlanName `
        --resource-group $ResourceGroup `
        --location $Location `
        --sku B1 `
        --is-linux false
    if ($LASTEXITCODE -ne 0) { throw "Failed to create App Service plan." }

    & $az webapp create `
        --name $AppName `
        --resource-group $ResourceGroup `
        --plan $PlanName `
        --runtime "dotnet:10"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Retrying with DOTNETCORE:9 runtime if .NET 10 unavailable..." -ForegroundColor Yellow
        & $az webapp create `
            --name $AppName `
            --resource-group $ResourceGroup `
            --plan $PlanName `
            --runtime "DOTNETCORE:9"
        if ($LASTEXITCODE -ne 0) { throw "Failed to create web app." }
    }
}

Write-Host "Applying application settings..." -ForegroundColor Cyan
& $az webapp config appsettings set `
    --name $AppName `
    --resource-group $ResourceGroup `
    --settings `
        ASPNETCORE_ENVIRONMENT=Production `
        AzureStorage__ContainerName=venue-images `
        ConnectionStrings__DefaultConnection=$AzureSqlConnection `
        AzureStorage__ConnectionString=$AzureStorageConnection
if ($LASTEXITCODE -ne 0) { throw "Failed to set app settings." }

Write-Host "Deploying zip package..." -ForegroundColor Cyan
& $az webapp deploy `
    --resource-group $ResourceGroup `
    --name $AppName `
    --src-path $ZipPath `
    --type zip `
    --async false
if ($LASTEXITCODE -ne 0) { throw "Zip deploy failed." }

& $az webapp restart --name $AppName --resource-group $ResourceGroup

$url = "https://$AppName.azurewebsites.net"
Write-Host ""
Write-Host "Deployment complete: $url" -ForegroundColor Green
Write-Host "Save this URL for your POE submission." -ForegroundColor Green
