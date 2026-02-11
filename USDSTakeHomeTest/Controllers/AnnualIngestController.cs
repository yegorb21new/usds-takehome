using Microsoft.AspNetCore.Mvc;
using USDSTakeHomeTest.Services;

namespace USDSTakeHomeTest.Controllers;

[ApiController]
[Route("api/ingest")]
public class AnnualIngestController : ControllerBase
{
    private readonly AnnualCfrIngestService _annual;

    public AnnualIngestController(AnnualCfrIngestService annual)
    {
        _annual = annual;
    }

    // POST /api/ingest/annual?year=2023&fromTitle=1&toTitle=50
    [HttpPost("annual")]
    public async Task<IActionResult> IngestAnnual(
        [FromQuery] int year,
        [FromQuery] int fromTitle = 1,
        [FromQuery] int toTitle = 50,
        CancellationToken ct = default)
    {
        if (year <= 0)
            return BadRequest(new { Message = "year is required, e.g. year=2023" });

        if (fromTitle < 1 || fromTitle > 50 || toTitle < 1 || toTitle > 50)
            return BadRequest(new { Message = "Titles must be between 1 and 50." });

        if (fromTitle > toTitle)
            return BadRequest(new { Message = "fromTitle must be <= toTitle." });

        var result = await _annual.IngestYearAsync(year, fromTitle, toTitle, ct);

        return Ok(new
        {
            Message = "Annual ingest complete.",
            result.SnapshotId,
            result.Year,
            result.FromTitle,
            result.ToTitle,
            result.TotalVolumes,
            result.TotalChaptersParsed,
            result.TotalAgenciesUpserted,
            result.PerTitle
        });
    }
}
