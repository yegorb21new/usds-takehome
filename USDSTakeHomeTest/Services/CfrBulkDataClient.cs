using System.Net;
using System.Text.RegularExpressions;

namespace USDSTakeHomeTest.Services;

public class CfrBulkDataClient
{
    private readonly IHttpClientFactory _httpClientFactory;

    public CfrBulkDataClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string BuildTitleDirectoryUrl(int year, int title)
        => $"https://www.govinfo.gov/bulkdata/CFR/{year}/title-{title}";

    public async Task<List<(string Name, string Link)>> ListTitleVolumeXmlAsync(int year, int title, CancellationToken ct)
    {
        var directoryUrl = BuildTitleDirectoryUrl(year, title);

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(2);

        using var resp = await client.GetAsync(directoryUrl, ct);

        // If the directory doesn't exist for that year/title, just return empty (caller will skip)
        if (resp.StatusCode == HttpStatusCode.NotFound)
            return new List<(string Name, string Link)>();

        resp.EnsureSuccessStatusCode();

        var html = await resp.Content.ReadAsStringAsync(ct);

        // Example filenames:
        // CFR-2024-title1-vol1.xml
        // CFR-2024-title12-vol3.xml
        var pattern = $@"CFR-{year}-title{title}-vol\d+\.xml";
        var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase);

        // De-dupe and build absolute URLs
        var fileNames = matches
            .Select(m => m.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var results = new List<(string Name, string Link)>(fileNames.Count);
        foreach (var file in fileNames)
        {
            // On GovInfo, the files live under the same directory URL
            var link = $"{directoryUrl}/{file}";
            results.Add((file, link));
        }

        return results;
    }

    public async Task<Stream> DownloadXmlAsync(string url, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(5);

        var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        return await resp.Content.ReadAsStreamAsync(ct);
    }
}
