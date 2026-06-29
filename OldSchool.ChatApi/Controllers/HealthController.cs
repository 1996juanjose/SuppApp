using Microsoft.AspNetCore.Mvc;

namespace OldSchool.ChatApi.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "Healthy", service = "OldSchool.ChatApi" });
}