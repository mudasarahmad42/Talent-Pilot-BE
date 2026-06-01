using System.Data;

namespace TalentPilot.Application.Documents;

public sealed record DocumentExportFile(
    string FileName,
    string ContentType,
    byte[] Content);

public sealed record ExcelWorksheetData(
    string Name,
    DataTable Table);
