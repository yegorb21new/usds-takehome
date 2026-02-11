using System.Net;

namespace USDSTakeHomeTest.Services;

public class EcfrDownloader
{
    private readonly IHttpClientFactory _httpClientFactory;

    public EcfrDownloader(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string BuildGovInfoEcfrTitleUrl(int titleNumber)
        => $"https://www.govinfo.gov/bulkdata/ECFR/title-{titleNumber}/ECFR-title{titleNumber}.xml";

    public async Task<Stream> DownloadTitleXmlAsync(int titleNumber, CancellationToken ct)
    {
        var url = BuildGovInfoEcfrTitleUrl(titleNumber);

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(5);

        // ResponseHeadersRead so we can stream XML without buffering the whole file
        var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);

        if (resp.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException($"Title {titleNumber} not found at {url}");

        resp.EnsureSuccessStatusCode();

        return await resp.Content.ReadAsStreamAsync(ct);
    }
}
