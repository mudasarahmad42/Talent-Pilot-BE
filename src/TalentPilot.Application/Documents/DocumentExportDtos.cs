using System.Data;

namespace TalentPilot.Application.Documents;

public sealed record DocumentExportFile(
    string FileName,
    string ContentType,
    byte[] Content);

public sealed record ExcelWorksheetData(
    string Name,
    DataTable Table);

public sealed record WordParagraphData(
    string Text,
    WordParagraphStyle Style = WordParagraphStyle.Normal,
    bool IsBullet = false);

public enum WordParagraphStyle
{
    Normal,
    Title,
    Heading1,
    Heading2
}
