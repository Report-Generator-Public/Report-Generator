using System.Net.Mime;
using DomainObjects.Models;
using Microsoft.AspNetCore.Mvc;
using Pdf;

namespace PdfGenerator.API.Controllers;

[ApiController]
[Route("api/[action]")]
public class PdfController : ControllerBase
{
    private readonly ReportGeneration _reportGeneration;

    public PdfController(ReportGeneration reportGeneration)
    {
        _reportGeneration = reportGeneration;
    }

    [HttpPost]
    public async Task<IActionResult> Generate([FromBody] ReportData data)
    {
        var bytes = await _reportGeneration.GenerateReport(data);
        if (bytes is null || !bytes.Any())
        {
            return BadRequest("Failed to generate pdf");
        }

        return Ok(bytes);
    }

    [HttpPost]
    public async Task<IActionResult> GenerateFile([FromBody] ReportData data)
    {
        var bytes = await _reportGeneration.GenerateReport(data);
        if (bytes is null || !bytes.Any())
        {
            return BadRequest("Failed to generate pdf");
        }

        var memoryStream = new MemoryStream(bytes);
        Response.Headers.Add("content-disposition",
            $"{DispositionTypeNames.Attachment}; filename={data.Sample.LabelId.ToUpper()}.pdf");
        return new FileStreamResult(memoryStream, "application/pdf");
    }
}