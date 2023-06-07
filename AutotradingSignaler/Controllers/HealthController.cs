using Microsoft.AspNetCore.Mvc;

namespace AutotradingSignaler.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{

    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok();
    }

}