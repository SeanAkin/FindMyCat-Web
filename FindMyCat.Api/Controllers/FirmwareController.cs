using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FindMyCat.Api.Controllers;

public sealed record FirmwareSettings(string Directory);

[ApiController]
[AllowAnonymous]
public partial class FirmwareController(FirmwareSettings settings) : ControllerBase
{
    [HttpGet("api/firmware/{file}")]
    // Collars flashed before the move ask for their update here; drop once none are left.
    [HttpGet("api/device-commands/firmware/{file}")]
    public IActionResult Download(string file)
    {
        var path = Path.Combine(settings.Directory, file);
        if (!FileName().IsMatch(file) || !System.IO.File.Exists(path))
        {
            return NotFound();
        }

        // Ranges let the collar resume a download after a dropped connection.
        return PhysicalFile(path, "application/octet-stream", enableRangeProcessing: true);
    }

    [GeneratedRegex("^[A-Za-z0-9_][A-Za-z0-9_.-]*$")]
    private static partial Regex FileName();
}