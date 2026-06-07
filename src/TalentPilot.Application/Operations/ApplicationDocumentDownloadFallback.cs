using System.Text;

namespace TalentPilot.Application.Operations;

public sealed record ApplicationDocumentTextFallbackDownload(
    string FileName,
    string ContentType,
    byte[] Content);

public static class ApplicationDocumentDownloadFallback
{
    public const string PlainTextContentType = "text/plain; charset=utf-8";

    public static ApplicationDocumentTextFallbackDownload? FromExtractedText(
        string fileName,
        string documentType,
        string extractionStatus,
        string? parserVersion,
        DateTimeOffset? extractedAt,
        string? extractedText)
    {
        if (string.IsNullOrWhiteSpace(extractedText))
        {
            return null;
        }

        var content = BuildContent(
            fileName,
            documentType,
            extractionStatus,
            parserVersion,
            extractedAt,
            extractedText.Trim());

        return new ApplicationDocumentTextFallbackDownload(
            BuildFileName(fileName, documentType),
            PlainTextContentType,
            Encoding.UTF8.GetBytes(content));
    }

    private static string BuildContent(
        string fileName,
        string documentType,
        string extractionStatus,
        string? parserVersion,
        DateTimeOffset? extractedAt,
        string extractedText)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Parsed document text");
        builder.AppendLine($"Original file: {CleanLine(fileName, "Application document")}");
        builder.AppendLine($"Document type: {CleanLine(documentType, "Application document")}");
        builder.AppendLine($"Extraction status: {CleanLine(extractionStatus, "Extracted")}");
        if (!string.IsNullOrWhiteSpace(parserVersion))
        {
            builder.AppendLine($"Parser version: {parserVersion.Trim()}");
        }
        if (extractedAt is not null)
        {
            builder.AppendLine($"Extracted at: {extractedAt.Value:O}");
        }

        builder.AppendLine();
        builder.AppendLine(extractedText);
        return builder.ToString();
    }

    private static string BuildFileName(string fileName, string documentType)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = string.IsNullOrWhiteSpace(documentType) ? "application-document" : documentType.Trim();
        }

        var invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
        var sanitized = new string(baseName
            .Where(character => !invalidCharacters.Contains(character))
            .ToArray())
            .Trim();

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "application-document";
        }

        return $"{sanitized}-parsed-text.txt";
    }

    private static string CleanLine(string value, string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return normalized.ReplaceLineEndings(" ");
    }
}
