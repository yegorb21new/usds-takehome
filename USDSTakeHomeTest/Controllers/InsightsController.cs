using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using USDSTakeHomeTest.Data;

namespace USDSTakeHomeTest.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InsightsController : ControllerBase
{
    private readonly AppDbContext _db;

    public InsightsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Returns top "movers" between two snapshots.
    /// If fromSnapshotId/toSnapshotId are omitted, uses the most recent two snapshots.
    /// </summary>
    [HttpGet("top-changes")]
    public async Task<IActionResult> GetTopChanges(
        [FromQuery] int? fromSnapshotId,
        [FromQuery] int? toSnapshotId,
        [FromQuery] int top = 10,
        CancellationToken ct = default)
    {
        if (top < 1) top = 1;
        if (top > 100) top = 100;

        // Choose snapshots: explicit if provided; otherwise latest two.
        int fromId;
        int toId;

        if (fromSnapshotId.HasValue && toSnapshotId.HasValue)
        {
            fromId = fromSnapshotId.Value;
            toId = toSnapshotId.Value;
        }
        else
        {
            var latestTwo = await _db.Snapshots
                .OrderByDescending(s => s.SnapshotDate)
                .ThenByDescending(s => s.IngestedAt)
                .Select(s => new { s.Id, s.Type, s.SnapshotDate, s.IngestedAt })
                .Take(2)
                .ToListAsync(ct);

            if (latestTwo.Count < 2)
                return BadRequest(new { Message = "Need at least two snapshots to compute changes." });

            // latestTwo[0] is most recent; latestTwo[1] is previous
            toId = latestTwo[0].Id;
            fromId = latestTwo[1].Id;
        }

        var fromSnap = await _db.Snapshots
            .Where(s => s.Id == fromId)
            .Select(s => new { s.Id, s.Type, s.SnapshotDate })
            .FirstOrDefaultAsync(ct);

        var toSnap = await _db.Snapshots
            .Where(s => s.Id == toId)
            .Select(s => new { s.Id, s.Type, s.SnapshotDate })
            .FirstOrDefaultAsync(ct);

        if (fromSnap is null || toSnap is null)
            return NotFound(new { Message = "One or both snapshot ids not found." });

        // Pull metrics for each snapshot into memory (small enough: ~few hundred agencies)
        var fromMetrics = await _db.AgencyMetrics
            .Where(m => m.SnapshotId == fromId)
            .Select(m => new
            {
                m.AgencyId,
                m.WordCount,
                m.ObligationIntensity,
                m.Sha256Checksum
            })
            .ToListAsync(ct);

        var toMetrics = await _db.AgencyMetrics
            .Where(m => m.SnapshotId == toId)
            .Select(m => new
            {
                m.AgencyId,
                m.WordCount,
                m.ObligationIntensity,
                m.Sha256Checksum
            })
            .ToListAsync(ct);

        var fromByAgency = await _db.AgencyMetrics
            .Where(m => m.SnapshotId == fromId)
            .GroupBy(m => m.AgencyId)
            .Select(g => g
                .OrderByDescending(x => x.WordCount)
                .Select(x => new
                {
                    x.AgencyId,
                    x.WordCount,
                    x.ObligationIntensity,
                    x.Sha256Checksum
                })
                .First())
            .ToDictionaryAsync(x => x.AgencyId, x => x, ct);

        var toByAgency = await _db.AgencyMetrics
            .Where(m => m.SnapshotId == toId)
            .GroupBy(m => m.AgencyId)
            .Select(g => g
                .OrderByDescending(x => x.WordCount)
                .Select(x => new
                {
                    x.AgencyId,
                    x.WordCount,
                    x.ObligationIntensity,
                    x.Sha256Checksum
                })
                .First())
            .ToDictionaryAsync(x => x.AgencyId, x => x, ct);


        var agencies = await _db.Agencies
            .Select(a => new { a.Id, a.Name })
            .ToListAsync(ct);

        var rows = agencies.Select(a =>
        {
            fromByAgency.TryGetValue(a.Id, out var f);
            toByAgency.TryGetValue(a.Id, out var t);

            int fromWord = f?.WordCount ?? 0;
            int toWord = t?.WordCount ?? 0;

            double fromObl = f?.ObligationIntensity ?? 0.0;
            double toObl = t?.ObligationIntensity ?? 0.0;

            string? fromHash = f?.Sha256Checksum;
            string? toHash = t?.Sha256Checksum;

            return new
            {
                AgencyId = a.Id,
                AgencyName = a.Name,
                FromWordCount = fromWord,
                ToWordCount = toWord,
                DeltaWordCount = toWord - fromWord,
                FromObligationIntensity = fromObl,
                ToObligationIntensity = toObl,
                DeltaObligationIntensity = toObl - fromObl,
                ChecksumChanged = (fromHash != null && toHash != null) && !string.Equals(fromHash, toHash, StringComparison.OrdinalIgnoreCase),
                PresentInFrom = f != null,
                PresentInTo = t != null
            };
        }).ToList();

        var topWordIncreases = rows
            .OrderByDescending(x => x.DeltaWordCount)
            .ThenBy(x => x.AgencyName)
            .Take(top)
            .ToList();

        var topWordDecreases = rows
            .OrderBy(x => x.DeltaWordCount)
            .ThenBy(x => x.AgencyName)
            .Take(top)
            .ToList();

        var topObligationIncreases = rows
            .OrderByDescending(x => x.DeltaObligationIntensity)
            .ThenBy(x => x.AgencyName)
            .Take(top)
            .ToList();

        var topObligationDecreases = rows
            .OrderBy(x => x.DeltaObligationIntensity)
            .ThenBy(x => x.AgencyName)
            .Take(top)
            .ToList();

        return Ok(new
        {
            FromSnapshot = fromSnap,
            ToSnapshot = toSnap,
            Top = top,
            TopWordCountIncreases = topWordIncreases,
            TopWordCountDecreases = topWordDecreases,
            TopObligationIntensityIncreases = topObligationIncreases,
            TopObligationIntensityDecreases = topObligationDecreases
        });
    }
}
