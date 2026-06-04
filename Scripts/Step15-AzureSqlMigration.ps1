
param(
    [string]$AzureConnectionString = $env:EVENTEASE_AZURE_SQL,
    [string]$LocalServer = "(localdb)\mssqllocaldb",
    [string]$LocalDatabase = "EventEase",
    [switch]$DataOnly
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$dataScript = Join-Path $PSScriptRoot "eventease-azure-data.sql"

function Format-SqlString([string]$value) {
    if ($null -eq $value) { return "NULL" }
    return "N'" + ($value -replace "'", "''") + "'"
}

function Format-SqlDateTime($value) {
    if ($null -eq $value) { return "NULL" }
    return "'" + $value.ToString("yyyy-MM-dd HH:mm:ss") + "'"
}

function Get-QueryRows {
    param(
        [string]$ConnectionString,
        [string]$Query
    )

    $rows = New-Object System.Collections.Generic.List[object]
    $connection = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
    $connection.Open()
    try {
        $command = $connection.CreateCommand()
        $command.CommandText = $Query
        $reader = $command.ExecuteReader()
        while ($reader.Read()) {
            $row = @{}
            for ($i = 0; $i -lt $reader.FieldCount; $i++) {
                $name = $reader.GetName($i)
                $row[$name] = if ($reader.IsDBNull($i)) { $null } else { $reader.GetValue($i) }
            }
            $rows.Add([PSCustomObject]$row)
        }
        $reader.Close()
    }
    finally {
        $connection.Close()
    }

    return $rows
}

if ([string]::IsNullOrWhiteSpace($AzureConnectionString)) {
    Write-Host "Azure connection string required." -ForegroundColor Yellow
    Write-Host "Set EVENTEASE_AZURE_SQL or use -AzureConnectionString" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Azure Portal checklist:" -ForegroundColor Cyan
    Write-Host "  1. Create SQL Server + SQL Database (e.g. EventEaseDb)"
    Write-Host "  2. Server firewall: allow your IP + Allow Azure services"
    Write-Host "  3. Copy ADO.NET connection string from database Overview"
    exit 1
}

if (-not $DataOnly) {
    Write-Host "Applying EF migrations to Azure SQL..." -ForegroundColor Cyan
    Push-Location $projectRoot
    try {
        dotnet ef database update --connection $AzureConnectionString
        if ($LASTEXITCODE -ne 0) { throw "dotnet ef database update failed." }
    }
    finally {
        Pop-Location
    }
}

$localConnectionString = "Server=$LocalServer;Database=$LocalDatabase;Trusted_Connection=True;TrustServerCertificate=True;"

Write-Host "Exporting LocalDB data..." -ForegroundColor Cyan
$venues = Get-QueryRows -ConnectionString $localConnectionString -Query "SELECT * FROM Venues"
$events = Get-QueryRows -ConnectionString $localConnectionString -Query "SELECT * FROM Events"
$bookings = Get-QueryRows -ConnectionString $localConnectionString -Query "SELECT * FROM Bookings"

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("SET NOCOUNT ON;")
$lines.Add("-- EventTypes are seeded by EF migration; do not re-insert.")
$lines.Add("")

if ($venues.Count -gt 0) {
    $lines.Add("SET IDENTITY_INSERT Venues ON;")
    foreach ($v in $venues) {
        $lines.Add(
            "INSERT INTO Venues (VenueId, Name, Location, Capacity, Description, ImageUrl) VALUES (" +
            "$($v.VenueId), $(Format-SqlString $v.Name), $(Format-SqlString $v.Location), $($v.Capacity), " +
            "$(Format-SqlString $v.Description), $(Format-SqlString $v.ImageUrl));"
        )
    }
    $lines.Add("SET IDENTITY_INSERT Venues OFF;")
    $lines.Add("")
}

if ($events.Count -gt 0) {
    $lines.Add("SET IDENTITY_INSERT Events ON;")
    foreach ($e in $events) {
        $lines.Add(
            "INSERT INTO Events (EventId, Name, Description, PlannedStartDate, PlannedEndDate, EventTypeId) VALUES (" +
            "$($e.EventId), $(Format-SqlString $e.Name), $(Format-SqlString $e.Description), " +
            "$(Format-SqlDateTime $e.PlannedStartDate), $(Format-SqlDateTime $e.PlannedEndDate), $($e.EventTypeId));"
        )
    }
    $lines.Add("SET IDENTITY_INSERT Events OFF;")
    $lines.Add("")
}

if ($bookings.Count -gt 0) {
    $lines.Add("SET IDENTITY_INSERT Bookings ON;")
    foreach ($b in $bookings) {
        $lines.Add(
            "INSERT INTO Bookings (BookingId, VenueId, EventId, StartDateTime, EndDateTime, CreatedAt, Status) VALUES (" +
            "$($b.BookingId), $($b.VenueId), $($b.EventId), $(Format-SqlDateTime $b.StartDateTime), " +
            "$(Format-SqlDateTime $b.EndDateTime), $(Format-SqlDateTime $b.CreatedAt), $(Format-SqlString $b.Status));"
        )
    }
    $lines.Add("SET IDENTITY_INSERT Bookings OFF;")
}

$lines | Set-Content -Path $dataScript -Encoding UTF8
Write-Host "Wrote $dataScript" -ForegroundColor Green

$builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $AzureConnectionString
$sqlcmdArgs = @(
    "-S", $builder.DataSource,
    "-d", $builder.InitialCatalog,
    "-i", $dataScript,
    "-b"
)
if (-not [string]::IsNullOrWhiteSpace($builder.UserID)) {
    $sqlcmdArgs += @("-U", $builder.UserID, "-P", $builder.Password)
}
else {
    $sqlcmdArgs += "-E"
}

Write-Host "Importing data into Azure SQL..." -ForegroundColor Cyan
& sqlcmd @sqlcmdArgs -N -C
if ($LASTEXITCODE -ne 0) { throw "sqlcmd import failed." }

Write-Host "Verifying row counts on Azure..." -ForegroundColor Cyan
$verifyQuery = @"
SET NOCOUNT ON;
SELECT 'EventTypes' AS T, COUNT(*) AS C FROM EventTypes
UNION ALL SELECT 'Venues', COUNT(*) FROM Venues
UNION ALL SELECT 'Events', COUNT(*) FROM Events
UNION ALL SELECT 'Bookings', COUNT(*) FROM Bookings;
"@
$verifyArgs = @("-S", $builder.DataSource, "-d", $builder.InitialCatalog, "-Q", $verifyQuery, "-W", "-N", "-C")
if (-not [string]::IsNullOrWhiteSpace($builder.UserID)) {
    $verifyArgs += @("-U", $builder.UserID, "-P", $builder.Password)
}
else {
    $verifyArgs += "-E"
}
& sqlcmd @verifyArgs
if ($LASTEXITCODE -ne 0) { throw "Verification query failed." }

Write-Host "Step 15 complete: schema and data are on Azure SQL." -ForegroundColor Green
