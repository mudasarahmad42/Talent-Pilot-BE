namespace TalentPilot.Application.Documents;

public interface IDocumentExportService
{
    DocumentExportFile CreateExcelWorkbook(string fileName, IReadOnlyList<ExcelWorksheetData> worksheets);

    DocumentExportFile CreateWordDocument(string fileName, IReadOnlyList<WordParagraphData> paragraphs);
}
