using Microsoft.AspNetCore.Mvc;
using DiagramSample.Services;

namespace DiagramSample.Controllers;

[ApiController]
[Route("api/diagram")]
public class DiagramController : ControllerBase
{
    private readonly DiagramConfigLoader _loader;

    public DiagramController(DiagramConfigLoader loader)
    {
        _loader = loader;
    }

    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        try
        {
            var config = _loader.LoadConfig();
            return Ok(config);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("graph")]
    public IActionResult GetGraph()
    {
        try
        {
            var graph = _loader.BuildGraph();
            return Ok(graph);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("processes")]
    public IActionResult GetProcesses()
    {
        try
        {
            var procs = _loader.GetProcesses();
            return Ok(procs);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
