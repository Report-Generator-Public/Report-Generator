using Automation.Models;
using Automation.Models.Settings;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Automation.Services;

public sealed class BlobService
{
    private readonly StorageSettings _storageSettings;
    private readonly ILogger<BlobService> _logger;

    public BlobService(ILogger<BlobService> logger, IOptions<StorageSettings> blobOptions)
    {
        _storageSettings = blobOptions.Value;
        _logger = logger;
    }

    public async Task<BlobFile?> GetFileAsByteArray(string container, string fileName)
    {
        try
        {
            BlobServiceClient blobServiceClient = new BlobServiceClient(_storageSettings.ConnectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(container);
            BlobClient blobClient = containerClient.GetBlobClient(fileName);
            if (await blobClient.ExistsAsync())
            {
                using var ms = new MemoryStream();
                await blobClient.DownloadToAsync(ms);
                return new BlobFile { Name = fileName, Bytes = ms.ToArray() };
            }
            _logger.LogError("Specified blob container or file does not exist. Container: {container} Filename: {attachmentName}", container, fileName);
            return null;
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Could not download email attachment: {fileName}");
            return null;
        }
    }
}