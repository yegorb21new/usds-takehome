using Microsoft.EntityFrameworkCore;
using USDSTakeHomeTest.Data;
using USDSTakeHomeTest.Models;

namespace USDSTakeHomeTest.Services;

public class CurrentEcfrIngestService
{
    private readonly AppDbContext _db;
    private readonly EcfrDownloader _downloader;
    private readonly EcfrParser _parser;
    private readonly MetricsCalculator _metrics;

    public CurrentEcfrIngestService(
        AppDbContext db,
        EcfrDownloader downloader,
        EcfrParser parser,
        MetricsCalculator metrics)
    {
        _db = db;
        _downloader = downloader;
        _parser = parser;
        _metrics = metrics;
    }

    public sealed record TitleResult(int Title, int ChaptersParsed, string SourceUrl);

    public sealed record IngestRunResult(
        int SnapshotId,
        DateOnly SnapshotDate,
        int FromTitle,
        int ToTitle,
        int TotalChaptersParsed,
        int TotalAgenciesUpserted,
        List<TitleResult> PerTitle);

    /// <summary>
    /// Ingests current eCFR for a range of titles into ONE Snapshot.
    /// </summary>
    public async Task<IngestRunResult> IngestRangeAsync(int fromTitle, int toTitle, CancellationToken ct)
    {
        if (fromTitle < 1 || fromTitle > 50) throw new ArgumentOutOfRangeException(nameof(fromTitle));
        if (toTitle < 1 || toTitle > 50) throw new ArgumentOutOfRangeException(nameof(toTitle));
        if (fromTitle > toTitle) throw new ArgumentException("fromTitle must be <= toTitle");

        var snapshotDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var snapshot = new Snapshot
        {
            Type = "CurrentECFR",
            SnapshotDate = snapshotDate,
            Source = $"GovInfo bulkdata ECFR titles {fromTitle}-{toTitle}",
            IngestedAt = DateTime.UtcNow
        };

        _db.Snapshots.Add(snapshot);
        await _db.SaveChangesAsync(ct);

        int totalChaptersParsed = 0;
        int totalAgenciesUpserted = 0;
        var perTitle = new List<TitleResult>();

        // Cache agencies in-memory by NormalizedName to avoid repeated DB hits
        var agencyCache = await _db.Agencies
            .AsNoTracking()
            .Select(a => new { a.Id, a.Name, a.NormalizedName })
            .ToDictionaryAsync(x => x.NormalizedName, x => (x.Id, x.Name), ct);

        for (int title = fromTitle; title <= toTitle; title++)
        {
            ct.ThrowIfCancellationRequested();

            var sourceUrl = _downloader.BuildGovInfoEcfrTitleUrl(title);

            int chaptersParsedForTitle = 0;

            await using var stream = await _downloader.DownloadTitleXmlAsync(title, ct);
            var chapters = await _parser.ExtractChaptersAsync(stream, ct);

            foreach (var ch in chapters)
            {
                ct.ThrowIfCancellationRequested();

                chaptersParsedForTitle++;
                totalChaptersParsed++;

                var agencyName = ch.ChapterName.Trim();
                var normalizedName = NormalizeAgencyKey(agencyName);

                int agencyId;
                if (!agencyCache.TryGetValue(normalizedName, out var cached))
                {
                    // New agency; insert
                    var agency = new Agency
                    {
                        Name = agencyName,
                        NormalizedName = normalizedName
                    };

                    _db.Agencies.Add(agency);
                    await _db.SaveChangesAsync(ct);

                    agencyId = agency.Id;
                    agencyCache[normalizedName] = (agency.Id, agency.Name);
                    totalAgenciesUpserted++;
                }
                else
                {
                    agencyId = cached.Id;
                }

                var m = _metrics.Compute(ch.Text);

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

            perTitle.Add(new TitleResult(title, chaptersParsedForTitle, sourceUrl));
        }

        return new IngestRunResult(
            SnapshotId: snapshot.Id,
            SnapshotDate: snapshotDate,
            FromTitle: fromTitle,
            ToTitle: toTitle,
            TotalChaptersParsed: totalChaptersParsed,
            TotalAgenciesUpserted: totalAgenciesUpserted,
            PerTitle: perTitle);
    }

    private static string NormalizeAgencyKey(string name)
    {
        var chars = name.Trim().ToLowerInvariant().ToCharArray();
        var sb = new System.Text.StringBuilder(chars.Length);

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
