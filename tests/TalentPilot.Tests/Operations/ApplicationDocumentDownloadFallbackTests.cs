using System.Text;
using TalentPilot.Application.Operations;

namespace TalentPilot.Tests.Operations;

public sealed class ApplicationDocumentDownloadFallbackTests
{
    [Fact]
    public void FromExtractedText_ReturnsTextDownload_WhenParsedTextExists()
    {
        var extractedAt = new DateTimeOffset(2026, 6, 7, 10, 30, 0, TimeSpan.Zero);

        var download = ApplicationDocumentDownloadFallback.FromExtractedText(
            "Amara_Haq_Java_Backend.docx",
            "CV",
            "Extracted",
            "docx-wordprocessingml-v1",
            extractedAt,
            "Java, Spring Boot, Kafka");

        Assert.NotNull(download);
        Assert.Equal("Amara_Haq_Java_Backend-parsed-text.txt", download.FileName);
        Assert.Equal(ApplicationDocumentDownloadFallback.PlainTextContentType, download.ContentType);

        var text = Encoding.UTF8.GetString(download.Content);
        Assert.Contains("Original file: Amara_Haq_Java_Backend.docx", text, StringComparison.Ordinal);
        Assert.Contains("Document type: CV", text, StringComparison.Ordinal);
        Assert.Contains("Extraction status: Extracted", text, StringComparison.Ordinal);
        Assert.Contains("Parser version: docx-wordprocessingml-v1", text, StringComparison.Ordinal);
        Assert.Contains("Extracted at: 2026-06-07T10:30:00.0000000+00:00", text, StringComparison.Ordinal);
        Assert.Contains("Java, Spring Boot, Kafka", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FromExtractedText_ReturnsNull_WhenParsedTextIsMissing()
    {
        var download = ApplicationDocumentDownloadFallback.FromExtractedText(
            "resume.docx",
            "Resume",
            "Pending",
            null,
            null,
            "   ");

        Assert.Null(download);
    }
}
