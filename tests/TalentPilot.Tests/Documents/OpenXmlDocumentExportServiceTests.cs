using System.Data;
using System.IO.Compression;
using TalentPilot.Application.Documents;
using TalentPilot.Infrastructure.Documents;

namespace TalentPilot.Tests.Documents;

public sealed class OpenXmlDocumentExportServiceTests
{
    [Fact]
    public void CreateExcelWorkbook_GeneratesXlsxPackageFromDataTable()
    {
        var table = new DataTable("Audit Logs");
        table.Columns.Add("Actor", typeof(string));
        table.Columns.Add("Count", typeof(int));
        table.Rows.Add("Mudasar Ahmad", 3);

        var service = new OpenXmlDocumentExportService();
        var file = service.CreateExcelWorkbook(
            "audit-logs",
            [new ExcelWorksheetData("Audit Logs", table)]);

        Assert.Equal("audit-logs.xlsx", file.FileName);
        Assert.Equal(OpenXmlDocumentExportService.ExcelContentType, file.ContentType);
        Assert.NotEmpty(file.Content);

        using var archive = new ZipArchive(new MemoryStream(file.Content), ZipArchiveMode.Read);
        Assert.NotNull(archive.GetEntry("[Content_Types].xml"));
        Assert.NotNull(archive.GetEntry("xl/workbook.xml"));
        var worksheet = archive.GetEntry("xl/worksheets/sheet1.xml");
        Assert.NotNull(worksheet);

        using var reader = new StreamReader(worksheet.Open());
        var xml = reader.ReadToEnd();
        Assert.Contains("Actor", xml);
        Assert.Contains("Mudasar Ahmad", xml);
        Assert.Contains("<v>3</v>", xml);
    }

    [Fact]
    public void CreateWordDocument_GeneratesDocxPackageFromParagraphs()
    {
        var service = new OpenXmlDocumentExportService();
        var file = service.CreateWordDocument(
            "interview-questions",
            [
                new WordParagraphData("Interview Questions", WordParagraphStyle.Title),
                new WordParagraphData("Technical Interview", WordParagraphStyle.Heading1),
                new WordParagraphData("How would you design a React component boundary?"),
                new WordParagraphData("Look for maintainability trade-offs.", IsBullet: true)
            ]);

        Assert.Equal("interview-questions.docx", file.FileName);
        Assert.Equal(OpenXmlDocumentExportService.WordContentType, file.ContentType);
        Assert.NotEmpty(file.Content);

        using var archive = new ZipArchive(new MemoryStream(file.Content), ZipArchiveMode.Read);
        Assert.NotNull(archive.GetEntry("[Content_Types].xml"));
        var document = archive.GetEntry("word/document.xml");
        Assert.NotNull(document);

        using var reader = new StreamReader(document.Open());
        var xml = reader.ReadToEnd();
        Assert.Contains("Interview Questions", xml);
        Assert.Contains("Technical Interview", xml);
        Assert.Contains("React component boundary", xml);
        Assert.Contains("Look for maintainability trade-offs", xml);
    }
}
