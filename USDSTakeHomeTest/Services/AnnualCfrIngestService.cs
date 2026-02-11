using Microsoft.EntityFrameworkCore;
using System.Text;
using USDSTakeHomeTest.Data;
using USDSTakeHomeTest.Models;

namespace USDSTakeHomeTest.Services;

public class AnnualCfrIngestService
{
    private readonly AppDbContext _db;
    private readonly CfrBulkDataClient _bulk;
    private readonly EcfrParser _parser;
    private readonly MetricsCalculator _metrics;

    public AnnualCfrIngestService(
        AppDbContext db,
        CfrBulkDataClient bulk,
        EcfrParser parser,
        MetricsCalculator metrics)
    {
        _db = db;
        _bulk = bulk;
        _parser = parser;
        _metrics = metrics;
    }

    public sealed record TitleAnnualResult(int Title, int Volumes, int ChaptersParsed);

    public sealed record AnnualRunResult(
        int SnapshotId,
        int Year,
        int FromTitle,
        int ToTitle,
        int TotalVolumes,
        int TotalChaptersParsed,
        int TotalAgenciesUpserted,
        List<TitleAnnualResult> PerTitle);

    public async Task<AnnualRunResult> IngestYearAsync(int year, int fromTitle, int toTitle, CancellationToken ct)
    {
        if (year < 1996 || year > DateTime.UtcNow.Year + 1)
            throw new ArgumentOutOfRangeException(nameof(year), "Year looks out of range.");

        if (fromTitle < 1 || fromTitle > 50) throw new ArgumentOutOfRangeException(nameof(fromTitle));
        if (toTitle < 1 || toTitle > 50) throw new ArgumentOutOfRangeException(nameof(toTitle));
        if (fromTitle > toTitle) throw new ArgumentException("fromTitle must be <= toTitle");

        var snapshot = new Snapshot
        {
            Type = "CFRAnnual",
            SnapshotDate = new DateOnly(year, 1, 1),
            Source = $"GovInfo CFR Annual bulkdata year {year}, titles {fromTitle}-{toTitle}",
            IngestedAt = DateTime.UtcNow
        };

        _db.Snapshots.Add(snapshot);
        await _db.SaveChangesAsync(ct);

        // Cache agencies for speed
        var agencyCache = await _db.Agencies
            .AsNoTracking()
            .Select(a => new { a.Id, a.Name, a.NormalizedName })
            .ToDictionaryAsync(x => x.NormalizedName, x => (x.Id, x.Name), ct);

        int totalVolumes = 0;
        int totalChaptersParsed = 0;
        int totalAgenciesUpserted = 0;

        var perTitle = new List<TitleAnnualResult>();

        for (int title = fromTitle; title <= toTitle; title++)
        {
            ct.ThrowIfCancellationRequested();

            var volumes = await _bulk.ListTitleVolumeXmlAsync(year, title, ct);

            // Some titles/years may not exist (not published / not present yet); just skip.
            if (volumes.Count == 0)
            {
                perTitle.Add(new TitleAnnualResult(title, Volumes: 0, ChaptersParsed: 0));
                continue;
            }

            totalVolumes += volumes.Count;

            // Aggregate chapter text across volumes
            var chapterTextByKey = new Dictionary<string, (string DisplayName, StringBuilder Text)>(StringComparer.OrdinalIgnoreCase);

            int chaptersParsedThisTitle = 0;

            foreach (var vol in volumes)
            {
                ct.ThrowIfCancellationRequested();

                await using var stream = await _bulk.DownloadXmlAsync(vol.Link, ct);
                var chapters = await _parser.ExtractChaptersAsync(stream, ct);

                chaptersParsedThisTitle += chapters.Count;
                totalChaptersParsed += chapters.Count;

                foreach (var ch in chapters)
                {
                    var displayName = ch.ChapterName.Trim();
                    var key = NormalizeAgencyKey(displayName);

                    if (!chapterTextByKey.TryGetValue(key, out var agg))
                    {
                        agg = (displayName, new StringBuilder());
                        chapterTextByKey[key] = agg;
                    }

                    agg.Text.Append(ch.Text);
                    agg.Text.Append(' ');
                }
            }

            // Persist metrics per aggregated chapter
            foreach (var kv in chapterTextByKey)
            {
                ct.ThrowIfCancellationRequested();

                var normalizedKey = kv.Key;
                var displayName = kv.Value.DisplayName;

                int agencyId;
                if (!agencyCache.TryGetValue(normalizedKey, out var cached))
                {
                    var agency = new Agency
                    {
                        Name = displayName,
                        NormalizedName = normalizedKey
                    };
                    _db.Agencies.Add(agency);
                    await _db.SaveChangesAsync(ct);

                    agencyId = agency.Id;
                    agencyCache[normalizedKey] = (agency.Id, agency.Name);
                    totalAgenciesUpserted++;
                }
                else
                {
                    agencyId = cached.Id;
                }

                var m = _metrics.Compute(kv.Value.Text.ToString());

                var existing = await _db.AgencyMetrics
                    .FirstOrDefaultAsync(x => x.AgencyId == agencyId && x.SnapshotId == snapshot.Id, ct);

                if (existing is null)
                {
                    _db.AgencyMetrics.Add(new AgencyMetric
                    {
                        AgencyId = agencyId,
                        SnapshotId = snapshot.Id,
                        WordCount = m.WordCount,
                        ObligationIntensity = m.ObligationIntensity,
                        Sha256Checksum = m.Sha256Checksum
                    });
                }
                else
                {
                    existing.WordCount = m.WordCount;
                    existing.ObligationIntensity = m.ObligationIntensity;
                    existing.Sha256Checksum = m.Sha256Checksum;
                }

            }

            await _db.SaveChangesAsync(ct);

            perTitle.Add(new TitleAnnualResult(title, volumes.Count, chaptersParsedThisTitle));
        }

        return new AnnualRunResult(
            SnapshotId: snapshot.Id,
            Year: year,
            FromTitle: fromTitle,
            ToTitle: toTitle,
            TotalVolumes: totalVolumes,
            TotalChaptersParsed: totalChaptersParsed,
            TotalAgenciesUpserted: totalAgenciesUpserted,
            PerTitle: perTitle);
    }

    private static string NormalizeAgencyKey(string name)
    {
        var chars = name.Trim().ToLowerInvariant().ToCharArray();
        var sb = new StringBuilder(chars.Length);

        bool lastDash = false;
        foreach (var c in chars)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                lastDash = false;
            }
            else
            {
                if (!lastDash)
                {
                    sb.Append('-');
                    lastDash = true;
                }
            }
        }

        var key = sb.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(key) ? "unknown-agency" : key;
    }
}
