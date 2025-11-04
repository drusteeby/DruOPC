namespace OpcPlc.Controllers;

using Microsoft.AspNetCore.Mvc;
using OpcPlc.Configuration;
using System;
using System.Threading.Tasks;

[ApiController]
[Route("pn.json")]
public class PnJsonController : ControllerBase
{
    private readonly OpcPlcConfiguration _config;

    public PnJsonController(OpcPlcConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Gets the pn.json file content.
    /// </summary>
    /// <returns>JSON file content or 404 if not found.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(string), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Get()
    {
        try
        {
            if (System.IO.File.Exists(_config.PnJson))
            {
                var content = await System.IO.File.ReadAllTextAsync(_config.PnJson).ConfigureAwait(false);
                return Content(content, "application/json");
            }
            return NotFound();
        }
        catch(Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
}

