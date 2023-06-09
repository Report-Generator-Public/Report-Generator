using System.Net.Http.Json;
using Automation.Models;
using Automation.Models.Settings;
using Automation.Services;
using Azure;
using Azure.Messaging;
using Azure.Messaging.EventGrid;
using DomainObjects.Constants;
using DomainObjects.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Automation;

public class TriggerGeneration
{
    private readonly LimsDataAccess _limsDataAccess;
    private readonly FunctionSettings _functionSettings;
    private readonly BlobService _blobService;
    private readonly ReportGeneratorSettings _reportGeneratorSettings;
    private readonly FileShareService _shareService;
    private readonly EventSettings _eventSettings;

    private readonly HttpClient _httpClient;
    private readonly ILogger<TriggerGeneration> _logger;

    public TriggerGeneration(LimsDataAccess limsDataAccess, IOptions<FunctionSettings> functionOptions, BlobService blobService,
        IOptions<ReportGeneratorSettings> reportOptions, ILogger<TriggerGeneration> logger, FileShareService shareService,
        IOptions<EventSettings> eventOptions)
    {
        _limsDataAccess = limsDataAccess;
        _blobService = blobService;
        _logger = logger;
        _shareService = shareService;
        _eventSettings = eventOptions.Value;
        _functionSettings = functionOptions.Value;
        _reportGeneratorSettings = reportOptions.Value;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_reportGeneratorSettings.Url),
            Timeout = TimeSpan.FromMinutes(5)
        };
        _httpClient.DefaultRequestHeaders.Add(GeneralConstants.ApiKeyHeader, _reportGeneratorSettings.Secret);
    }

    [Function(nameof(TriggerGeneration))]
    public async Task RunAsync([TimerTrigger("0 */15 * * * *", RunOnStartup = true)] MyInfo myTimer, FunctionContext context)
    {
        _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.UtcNow}");

        _logger.LogInformation("Retrieving data...");
        var limsResults = await _limsDataAccess.GetResultsToGenerate();

        if (limsResults is null || !limsResults.Any())
        {
            _logger.LogInformation("No records need report generation");
            _logger.LogInformation("Terminating process");
            return;
        }

        _logger.LogInformation("Retrieved {recordCount} records", limsResults.Count);

        _logger.LogInformation("Retrieving blob data...");

        var reportLogo = await _blobService.GetFileAsByteArray(_functionSettings.LogoContainerName, _functionSettings.LogoFileName);
        if (reportLogo is null)
        {
            _logger.LogError("Failed to retrieve report logo");
            _logger.LogInformation("Terminating process");
            return;
        }

        var footer = await GetFooter();
        if (footer is null)
        {
            _logger.LogError("Failed to generate footer");
            _logger.LogInformation("Terminating process");
            return;
        }

        _logger.LogInformation("Blobs retrieved");


        List<CloudEvent> eventsList = new List<CloudEvent>();

        _logger.LogInformation("Starting reporting process...");
        foreach (var limsResult in limsResults)
        {
            _logger.LogInformation("Processing URN: {urn}", limsResult.Sample.LabelId);
            limsResult.Footer = footer;
            limsResult.LogoBase64 = Convert.ToBase64String(reportLogo.Bytes);
            // TODO: remove this
            limsResult.ReportAddress = new()
            {
                Name = "asd",
                Country = "asd",
                Email = "asd",
                Line1 = "asd",
                Line2 = "asd",
                Phone = "asd",
                Postcode = "asd",
                Town = "asd"
            };
            var httpResponse = await _httpClient.PostAsJsonAsync(_reportGeneratorSettings.Endpoint, limsResult);
            if (!httpResponse.IsSuccessStatusCode)
            {
                var responseText = await httpResponse.Content.ReadAsStringAsync();
                _logger.LogInformation("Failed to process URN: {urn}", limsResult.Sample.LabelId);
                _logger.LogError(responseText);
                continue;
            }

            var pdfBytes = await httpResponse.Content.ReadAsByteArrayAsync();
            _logger.LogInformation("Report generated. Uploading report..");

            var fileToUpload = new BlobFile
            {
                Bytes = pdfBytes,
                Identifier = limsResult.Sample.LabelId,
                Name = $"{limsResult.Sample.LabelId}.pdf"
            };

            var fileUploadResult = await _shareService.UploadFile(fileToUpload);
            if (fileUploadResult is null || !fileUploadResult.IsSuccessCode())
            {
                _logger.LogError("Failed to upload report for URN: {urn}", limsResult.Sample.LabelId);
            }

            eventsList.Add(new CloudEvent(_eventSettings.Source, _eventSettings.Type, new
            {
                Urn = fileToUpload.Identifier,
                CompletionProcess = _functionSettings.ProcessName
            }));

            _logger.LogInformation("URN: {urn} has been processed", limsResult.Sample.LabelId);
        }

        if (!eventsList.Any())
        {
            var message = "No successful file uploads. Terminating process";
            _logger.LogError(message);
            throw new Exception(message);
        }

        _logger.LogInformation("Publishing {eventCount} completion events", eventsList.Count);

        var eventClient = new EventGridPublisherClient(
            new Uri(_eventSettings.Endpoint),
            new AzureKeyCredential(_eventSettings.AccessToken));
        var eventResponse = await eventClient.SendEventsAsync(eventsList);

        if (eventResponse.Status != 200 || eventResponse.Status != 201)
        {
            var message = "Failed to complete the records";
            _logger.LogError(message);
            throw new Exception(message);
        }

        _logger.LogInformation("Execution succeeded");
    }

    private async Task<Footer?> GetFooter()
    {
        var footerLogo =
            await _blobService.GetFileAsByteArray(_functionSettings.FooterLogoContainerName, _functionSettings.FooterLogoFileName);
        if (footerLogo is null)
        {
            _logger.LogError("Failed to retrieve report logo");
            _logger.LogInformation("Terminating process");
            return null;
        }

        return new Footer
        {
            ContactDetails = _functionSettings.Footer.ContactDetails,
            FooterContent = _functionSettings.Footer.Content,
            LogoBase64 = Convert.ToBase64String(footerLogo.Bytes, 0, footerLogo.Bytes.Length)
        };
    }
}