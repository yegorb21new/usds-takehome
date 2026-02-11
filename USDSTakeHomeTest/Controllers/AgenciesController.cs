using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using USDSTakeHomeTest.Data;

namespace USDSTakeHomeTest.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgenciesController : ControllerBase
{
    private readonly AppDbContext _db;

    public AgenciesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAgencies(CancellationToken ct)
    {
        var latestSnapshot = await _db.Snapshots
            .OrderByDescending(s => s.SnapshotDate)
            .ThenByDescending(s => s.IngestedAt)
            .Select(s => new { s.Id, s.SnapshotDate, s.Type })
            .FirstOrDefaultAsync(ct);

        if (latestSnapshot is null)
        {
            var agenciesOnly = await _db.Agencies
                .OrderBy(a => a.Name)
                .Select(a => new
                {
                    a.Id,
                    a.Name,
                    a.NormalizedName,
                    Latest = (object?)null
                })
                .ToListAsync(ct);

            return Ok(new
            {
                LatestSnapshot = (object?)null,
                Agencies = agenciesOnly
            });
        }

        // Pull metrics for latest snapshot and sort by WordCount desc
        var agenciesWithLatest = await _db.AgencyMetrics
            .Where(m => m.SnapshotId == latestSnapshot.Id)
            .Join(_db.Agencies,
                m => m.AgencyId,
                a => a.Id,
                (m, a) => new
                {
                    a.Id,
                    a.Name,
                    a.NormalizedName,
                    Latest = new
                    {
                        m.WordCount,
                        m.ObligationIntensity,
                        m.Sha256Checksum
                    }
                })
            .OrderByDescending(x => x.Latest.WordCount)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);

        return Ok(new
        {
            LatestSnapshot = latestSnapshot,
            Agencies = agenciesWithLatest
        });
    }

    [HttpGet("{agencyId:int}/metrics")]
    public async Task<IActionResult> GetAgencyMetrics(int agencyId, CancellationToken ct)
    {
        var exists = await _db.Agencies.AnyAsync(a => a.Id == agencyId, ct);
        if (!exists) return NotFound(new { Message = $"Agency {agencyId} not found." });

        var series = await _db.AgencyMetrics
            .Where(m => m.AgencyId == agencyId)
            .Join(_db.Snapshots,
                m => m.SnapshotId,
                s => s.Id,
                (m, s) => new
                {
                    s.Id,
                    s.Type,
                    s.SnapshotDate,
                    s.IngestedAt,
                    m.WordCount,
                    m.ObligationIntensity,
                    m.Sha256Checksum
                })
            .OrderBy(x => x.SnapshotDate)
            .ThenBy(x => x.IngestedAt)
            .ToListAsync(ct);

        return Ok(series);
    }
}
