using System.Text;
using System.Xml;

namespace USDSTakeHomeTest.Services;

public class EcfrParser
{
    public sealed record ChapterText(string ChapterName, string Text);

    /// <summary>
    /// Parses eCFR XML and returns one item per CHAPTER (treated as agency).
    /// </summary>
    public async Task<List<ChapterText>> ExtractChaptersAsync(Stream xmlStream, CancellationToken ct)
    {
        var chapters = new List<ChapterText>();

        var settings = new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = true
        };

        using var reader = XmlReader.Create(xmlStream, settings);

        bool inChapter = false;
        int chapterStartDepth = -1;

        string? chapterName = null;
        bool capturedHead = false;

        var sb = new StringBuilder(1024 * 64);

        while (await reader.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();

            if (reader.NodeType == XmlNodeType.Element)
            {
                // Detect start of a chapter block (agency-ish unit)
                var typeAttr = reader.GetAttribute("TYPE");
                if (!inChapter && string.Equals(typeAttr, "CHAPTER", StringComparison.OrdinalIgnoreCase))
                {
                    inChapter = true;
                    chapterStartDepth = reader.Depth;
                    chapterName = null;
                    capturedHead = false;
                    sb.Clear();
                    continue;
                }

                // Capture chapter name from the first <HEAD> encountered inside the chapter
                if (inChapter && !capturedHead && reader.Name.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
                {
                    // HEAD may be element content; read it
                    var head = await reader.ReadElementContentAsStringAsync();
                    head = head?.Trim();

                    if (!string.IsNullOrWhiteSpace(head))
                    {
                        chapterName = head;
                        capturedHead = true;
                        sb.Append(head);
                        sb.Append(' ');
                    }

                    continue;
                }
            }

            if (inChapter && (reader.NodeType == XmlNodeType.Text || reader.NodeType == XmlNodeType.CDATA))
            {
                var text = reader.Value;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.Append(text);
                    sb.Append(' ');
                }
            }

            if (inChapter && reader.NodeType == XmlNodeType.EndElement && reader.Depth == chapterStartDepth)
            {
                // End of chapter element
                inChapter = false;

                var finalName = !string.IsNullOrWhiteSpace(chapterName)
                    ? chapterName!.Trim()
                    : "Unknown Chapter";

                var finalText = sb.ToString().Trim();

                if (!string.IsNullOrWhiteSpace(finalText))
                {
                    chapters.Add(new ChapterText(finalName, finalText));
                }

                chapterStartDepth = -1;
                chapterName = null;
                capturedHead = false;
                sb.Clear();
            }
        }

        return chapters;
    }
}
