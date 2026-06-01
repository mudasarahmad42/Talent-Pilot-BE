using System.Data;
using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml;
using TalentPilot.Application.Documents;

namespace TalentPilot.Infrastructure.Documents;

public sealed partial class OpenXmlDocumentExportService : IDocumentExportService
{
    public const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public DocumentExportFile CreateExcelWorkbook(string fileName, IReadOnlyList<ExcelWorksheetData> worksheets)
    {
        if (worksheets.Count == 0)
        {
            throw new ArgumentException("At least one worksheet is required.", nameof(worksheets));
        }

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteContentTypes(archive, worksheets.Count);
            WriteRootRelationships(archive);
            WriteWorkbook(archive, worksheets);
            WriteWorkbookRelationships(archive, worksheets.Count);

            for (var index = 0; index < worksheets.Count; index++)
            {
                WriteWorksheet(archive, index + 1, worksheets[index].Table);
            }
        }

        return new DocumentExportFile(NormalizeFileName(fileName), ExcelContentType, stream.ToArray());
    }

    private static void WriteContentTypes(ZipArchive archive, int sheetCount)
    {
        using var writer = CreateXmlWriter(archive, "[Content_Types].xml");
        writer.WriteStartDocument();
        writer.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
        writer.WriteStartElement("Default");
        writer.WriteAttributeString("Extension", "rels");
        writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-package.relationships+xml");
        writer.WriteEndElement();
        writer.WriteStartElement("Default");
        writer.WriteAttributeString("Extension", "xml");
        writer.WriteAttributeString("ContentType", "application/xml");
        writer.WriteEndElement();
        writer.WriteStartElement("Override");
        writer.WriteAttributeString("PartName", "/xl/workbook.xml");
        writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
        writer.WriteEndElement();

        for (var sheetIndex = 1; sheetIndex <= sheetCount; sheetIndex++)
        {
            writer.WriteStartElement("Override");
            writer.WriteAttributeString("PartName", $"/xl/worksheets/sheet{sheetIndex}.xml");
            writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteRootRelationships(ZipArchive archive)
    {
        using var writer = CreateXmlWriter(archive, "_rels/.rels");
        writer.WriteStartDocument();
        writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
        writer.WriteStartElement("Relationship");
        writer.WriteAttributeString("Id", "rId1");
        writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument");
        writer.WriteAttributeString("Target", "xl/workbook.xml");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteWorkbook(ZipArchive archive, IReadOnlyList<ExcelWorksheetData> worksheets)
    {
        using var writer = CreateXmlWriter(archive, "xl/workbook.xml");
        writer.WriteStartDocument();
        writer.WriteStartElement("workbook", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        writer.WriteAttributeString("xmlns", "r", null, RelationshipNamespace);
        writer.WriteStartElement("sheets");

        for (var index = 0; index < worksheets.Count; index++)
        {
            writer.WriteStartElement("sheet");
            writer.WriteAttributeString("name", NormalizeWorksheetName(worksheets[index].Name, index + 1));
            writer.WriteAttributeString("sheetId", (index + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("r", "id", RelationshipNamespace, $"rId{index + 1}");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteWorkbookRelationships(ZipArchive archive, int sheetCount)
    {
        using var writer = CreateXmlWriter(archive, "xl/_rels/workbook.xml.rels");
        writer.WriteStartDocument();
        writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");

        for (var sheetIndex = 1; sheetIndex <= sheetCount; sheetIndex++)
        {
            writer.WriteStartElement("Relationship");
            writer.WriteAttributeString("Id", $"rId{sheetIndex}");
            writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet");
            writer.WriteAttributeString("Target", $"worksheets/sheet{sheetIndex}.xml");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteWorksheet(ZipArchive archive, int sheetIndex, DataTable table)
    {
        using var writer = CreateXmlWriter(archive, $"xl/worksheets/sheet{sheetIndex}.xml");
        writer.WriteStartDocument();
        writer.WriteStartElement("worksheet", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        writer.WriteStartElement("sheetData");
        WriteHeaderRow(writer, table);
        WriteDataRows(writer, table);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteHeaderRow(XmlWriter writer, DataTable table)
    {
        writer.WriteStartElement("row");
        writer.WriteAttributeString("r", "1");

        for (var columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
        {
            var column = table.Columns[columnIndex];
            var heading = string.IsNullOrWhiteSpace(column.Caption) ? column.ColumnName : column.Caption;
            WriteCell(writer, columnIndex, rowNumber: 1, heading);
        }

        writer.WriteEndElement();
    }

    private static void WriteDataRows(XmlWriter writer, DataTable table)
    {
        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            var rowNumber = rowIndex + 2;
            writer.WriteStartElement("row");
            writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));

            for (var columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
            {
                WriteCell(writer, columnIndex, rowNumber, table.Rows[rowIndex][columnIndex]);
            }

            writer.WriteEndElement();
        }
    }

    private static void WriteCell(XmlWriter writer, int columnIndex, int rowNumber, object? value)
    {
        writer.WriteStartElement("c");
        writer.WriteAttributeString("r", $"{ColumnName(columnIndex)}{rowNumber}");

        if (value is null or DBNull)
        {
            writer.WriteEndElement();
            return;
        }

        if (TryWriteScalarCell(writer, value))
        {
            writer.WriteEndElement();
            return;
        }

        writer.WriteAttributeString("t", "inlineStr");
        writer.WriteStartElement("is");
        writer.WriteStartElement("t");
        writer.WriteString(ToCellText(value));
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static bool TryWriteScalarCell(XmlWriter writer, object value)
    {
        switch (value)
        {
            case bool boolean:
                writer.WriteAttributeString("t", "b");
                writer.WriteElementString("v", boolean ? "1" : "0");
                return true;
            case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                writer.WriteElementString("v", Convert.ToString(value, CultureInfo.InvariantCulture));
                return true;
            default:
                return false;
        }
    }

    private static string ToCellText(object value)
    {
        var text = value switch
        {
            DateTimeOffset dateTimeOffset => dateTimeOffset.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };

        return RemoveInvalidXmlCharacters(text);
    }

    private static string RemoveInvalidXmlCharacters(string value)
    {
        return string.Create(
            value.Length,
            value,
            (buffer, source) =>
            {
                var next = 0;
                foreach (var character in source)
                {
                    if (XmlConvert.IsXmlChar(character))
                    {
                        buffer[next++] = character;
                    }
                }

                buffer[next..].Clear();
            }).TrimEnd('\0');
    }

    private static XmlWriter CreateXmlWriter(ZipArchive archive, string entryName)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        return XmlWriter.Create(entry.Open(), new XmlWriterSettings
        {
            CloseOutput = true,
            Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false
        });
    }

    private static string ColumnName(int zeroBasedIndex)
    {
        var dividend = zeroBasedIndex + 1;
        var columnName = string.Empty;

        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName;
    }

    private static string NormalizeFileName(string fileName)
    {
        var trimmed = string.IsNullOrWhiteSpace(fileName) ? "export.xlsx" : fileName.Trim();
        var safe = InvalidFileNameCharacterPattern().Replace(trimmed, "-");
        return safe.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ? safe : $"{safe}.xlsx";
    }

    private static string NormalizeWorksheetName(string name, int fallbackIndex)
    {
        var normalized = InvalidWorksheetNameCharacterPattern().Replace(name.Trim(), " ");
        normalized = string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = $"Sheet {fallbackIndex}";
        }

        return normalized[..Math.Min(31, normalized.Length)];
    }

    [GeneratedRegex("""[\\/:*?\[\]]""")]
    private static partial Regex InvalidWorksheetNameCharacterPattern();

    [GeneratedRegex("""[\\/:*?"<>|]""")]
    private static partial Regex InvalidFileNameCharacterPattern();
}
