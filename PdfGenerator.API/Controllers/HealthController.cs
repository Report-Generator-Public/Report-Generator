using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PdfGenerator.API.Controllers;

[ApiController]
[Route("api/[action]")]
public class HealthController : ControllerBase
{
    [HttpHead, AllowAnonymous]
    public IActionResult HealthCheck()
    {
        return Ok();
    }
}