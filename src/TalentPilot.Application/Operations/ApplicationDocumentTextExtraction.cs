using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using TalentPilot.Application.Ai;

namespace TalentPilot.Application.Operations;

public interface IApplicationDocumentTextExtractor
{
    ApplicationDocumentTextExtractionResult Extract(string fileName, byte[] content);
}

public sealed record ApplicationDocumentTextExtractionResult(
    string Status,
    string? ExtractedText,
    string? ExtractedTextHashSha256,
    string ParserVersion,
    DateTimeOffset? ExtractedAtUtc,
    string? Error);

public sealed class DocxApplicationDocumentTextExtractor : IApplicationDocumentTextExtractor
{
    public const string CurrentParserVersion = "docx-wordprocessingml-v1";

    public ApplicationDocumentTextExtractionResult Extract(string fileName, byte[] content)
    {
        if (!fileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
        {
            return new ApplicationDocumentTextExtractionResult(
                "Unsupported",
                null,
                null,
                CurrentParserVersion,
                null,
                "Only DOCX extraction is supported in MVP.");
        }

        try
        {
            using var stream = new MemoryStream(content);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            var entry = archive.GetEntry("word/document.xml");
            if (entry is null)
            {
                return new ApplicationDocumentTextExtractionResult(
                    "Failed",
                    null,
                    null,
                    CurrentParserVersion,
                    DateTimeOffset.UtcNow,
                    "DOCX document body was not found.");
            }

            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            var xml = XDocument.Parse(reader.ReadToEnd());
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var paragraphs = xml.Descendants(w + "p")
                .Select(paragraph => string.Concat(paragraph.Descendants(w + "t").Select(text => text.Value)).Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line));

            var text = string.Join('\n', paragraphs).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return new ApplicationDocumentTextExtractionResult(
                    "Failed",
                    null,
                    null,
                    CurrentParserVersion,
                    DateTimeOffset.UtcNow,
                    "No readable text was found in the DOCX document.");
            }

            var normalized = text.Length <= 40_000 ? text : text[..40_000];
            return new ApplicationDocumentTextExtractionResult(
                "Extracted",
                normalized,
                AiTextHasher.HashText(normalized),
                CurrentParserVersion,
                DateTimeOffset.UtcNow,
                null);
        }
        catch (Exception ex)
        {
            return new ApplicationDocumentTextExtractionResult(
                "Failed",
                null,
                null,
                CurrentParserVersion,
                DateTimeOffset.UtcNow,
                ex.Message.Length <= 1000 ? ex.Message : ex.Message[..1000]);
        }
    }
}
