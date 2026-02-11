using Microsoft.AspNetCore.Mvc;
using USDSTakeHomeTest.Services;

namespace USDSTakeHomeTest.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IngestController : ControllerBase
{
    private readonly CurrentEcfrIngestService _ingest;

    public IngestController(CurrentEcfrIngestService ingest)
    {
        _ingest = ingest;
    }

    [HttpPost("current")]
    public async Task<IActionResult> IngestCurrentRange(
        [FromQuery] int fromTitle = 1,
        [FromQuery] int toTitle = 1,
        CancellationToken ct = default)
    {
        if (fromTitle < 1 || fromTitle > 50 || toTitle < 1 || toTitle > 50)
            return BadRequest(new { Message = "Titles must be between 1 and 50." });

        if (fromTitle > toTitle)
            return BadRequest(new { Message = "fromTitle must be <= toTitle." });

        var result = await _ingest.IngestRangeAsync(fromTitle, toTitle, ct);

        return Ok(new
        {
            Message = "Ingest complete.",
            result.SnapshotId,
            result.SnapshotDate,
            result.FromTitle,
            result.ToTitle,
            result.TotalChaptersParsed,
            result.TotalAgenciesUpserted,
            result.PerTitle
        });
    }
}
