using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Data.SqlClient;

const string containerName = "venue-images";

var azureStorageConnection = Environment.GetEnvironmentVariable("EVENTEASE_AZURE_STORAGE");
var azureSqlConnection = Environment.GetEnvironmentVariable("EVENTEASE_AZURE_SQL");
var migrateFromAzurite = Environment.GetEnvironmentVariable("EVENTEASE_MIGRATE_AZURITE") == "true";

if (string.IsNullOrWhiteSpace(azureStorageConnection))
{
    Console.WriteLine("Set EVENTEASE_AZURE_STORAGE to your Azure Storage connection string.");
    Console.WriteLine("Optional: EVENTEASE_AZURE_SQL to rewrite venue ImageUrl values after blob copy.");
    Console.WriteLine("Optional: EVENTEASE_MIGRATE_AZURITE=true to copy blobs from local Azurite.");
    return 1;
}

var azureService = new BlobServiceClient(azureStorageConnection);
var azureContainer = azureService.GetBlobContainerClient(containerName);
await azureContainer.CreateIfNotExistsAsync(PublicAccessType.Blob);
Console.WriteLine($"Container '{containerName}' is ready (public blob read).");

if (migrateFromAzurite)
{
    var localService = new BlobServiceClient("UseDevelopmentStorage=true");
    var localContainer = localService.GetBlobContainerClient(containerName);

    if (!await localContainer.ExistsAsync())
    {
        Console.WriteLine("Azurite container not found; skipping blob copy.");
    }
    else
    {
        int copied = 0;
        await foreach (var item in localContainer.GetBlobsAsync())
        {
            var source = localContainer.GetBlobClient(item.Name);
            var target = azureContainer.GetBlobClient(item.Name);
            using var stream = await source.OpenReadAsync();
            await target.UploadAsync(stream, overwrite: true);
            copied++;
            Console.WriteLine($"Copied: {item.Name}");
        }

        Console.WriteLine($"Copied {copied} blob(s) from Azurite to Azure.");
    }
}

if (!string.IsNullOrWhiteSpace(azureSqlConnection))
{
    var accountName = ParseAccountName(azureStorageConnection);
    if (accountName is null)
    {
        Console.WriteLine("Could not parse storage account name; ImageUrl SQL update skipped.");
    }
    else
    {
        var azureBaseUrl = $"https://{accountName}.blob.core.windows.net/{containerName}/";
        await using var connection = new SqlConnection(azureSqlConnection);
        await connection.OpenAsync();

        const string updateSql = """
            UPDATE Venues
            SET ImageUrl = @AzureBase + SUBSTRING(ImageUrl, CHARINDEX('venue-images/', ImageUrl) + LEN('venue-images/'), LEN(ImageUrl))
            WHERE ImageUrl IS NOT NULL
              AND ImageUrl LIKE '%venue-images/%'
              AND ImageUrl NOT LIKE @AzureBase + '%';
            """;

        await using var command = new SqlCommand(updateSql, connection);
        command.Parameters.AddWithValue("@AzureBase", azureBaseUrl);
        var updated = await command.ExecuteNonQueryAsync();
        Console.WriteLine($"Updated {updated} venue ImageUrl row(s) to Azure blob URLs.");
    }
}

Console.WriteLine("Step 16 storage setup complete.");
return 0;

static string? ParseAccountName(string connectionString)
{
    foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
    {
        if (part.Trim().StartsWith("AccountName=", StringComparison.OrdinalIgnoreCase))
        {
            return part.Split('=', 2)[1].Trim();
        }
    }

    return null;
}
