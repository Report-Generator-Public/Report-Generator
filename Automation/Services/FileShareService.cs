using Automation.Models;
using Automation.Models.Settings;
using Azure.Storage.Files.Shares;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Automation.Services;

public sealed class FileShareService
{
    private readonly StorageSettings _settings;
    private readonly ILogger<FileShareService> _logger;

    public FileShareService(IOptions<StorageSettings> options, ILogger<FileShareService> logger)
    {
        _logger = logger;
        _settings = options.Value;
    }

    public async Task<BlobFile?> UploadFile(BlobFile fileToUpload)
    {
        var share = new ShareClient(_settings.ConnectionString, _settings.ReportUploadShareName);
        var clientDir = share.GetDirectoryClient(_settings.ReportUploadFolderName);
        await clientDir.CreateIfNotExistsAsync();

        try
        {
            var memoryStream = new MemoryStream(fileToUpload.Bytes);
            memoryStream.Position = 0;

            var fileClient = clientDir.GetFileClient(fileToUpload.Name);
            await fileClient.CreateAsync(memoryStream.Length);
            var resp = await fileClient.UploadAsync(memoryStream);
            var response = new BlobFile
            {
                Identifier = fileToUpload.Identifier,
                Name = fileToUpload.Name,
                ResponseCode = resp.GetRawResponse().Status
            };
            return response;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to upload files to file {fileName}  to the share: {share}.", fileToUpload.Name,
                _settings.ReportUploadShareName);
            return null;
        }
    }
}