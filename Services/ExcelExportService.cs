using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace ProductApp.Services;

/// <summary>
/// تصدير بيانات إلى ملف Excel (xlsx) بدون مكتبات خارجية.
/// </summary>
public static class ExcelExportService
{
    public static void Export(string filePath, string[] headers, IEnumerable<object?[]> rows)
    {
        var data = rows.ToList();

        using var stream = File.Create(filePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

        WriteEntry(archive, "[Content_Types].xml",
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""" +
            """<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">""" +
            """<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>""" +
            """<Default Extension="xml" ContentType="application/xml"/>""" +
            """<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>""" +
            """<Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>""" +
            """<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>""" +
            """</Types>""");

        WriteEntry(archive, "_rels/.rels",
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""" +
            """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">""" +
            """<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>""" +
            """</Relationships>""");

        WriteEntry(archive, "xl/workbook.xml",
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""" +
            """<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" """ +
            """xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">""" +
            """<sheets><sheet name="تقرير" sheetId="1" r:id="rId1"/></sheets></workbook>""");

        WriteEntry(archive, "xl/_rels/workbook.xml.rels",
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""" +
            """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">""" +
            """<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>""" +
            """<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>""" +
            """</Relationships>""");

        WriteEntry(archive, "xl/styles.xml",
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""" +
            """<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">""" +
            """<fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts>""" +
            """<fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>""" +
            """<borders count="1"><border/></borders>""" +
            """<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>""" +
            """<cellXfs count="3">""" +
            """<xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>""" +
            """<xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/>""" +
            """<xf numFmtId="2" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>""" +
            """</cellXfs></styleSheet>""");

        WriteSheet(archive, headers, data);
    }

    private static void WriteSheet(ZipArchive archive, string[] headers, List<object?[]> rows)
    {
        var entry = archive.CreateEntry("xl/worksheets/sheet1.xml");
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = Encoding.UTF8,
            OmitXmlDeclaration = false
        });

        writer.WriteStartDocument();
        writer.WriteStartElement("worksheet", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        writer.WriteStartElement("sheetData");

        // Header row
        writer.WriteStartElement("row");
        writer.WriteAttributeString("r", "1");
        for (int c = 0; c < headers.Length; c++)
            WriteCell(writer, 1, c + 1, headers[c], isHeader: true);
        writer.WriteEndElement();

        // Data rows
        for (int r = 0; r < rows.Count; r++)
        {
            writer.WriteStartElement("row");
            writer.WriteAttributeString("r", (r + 2).ToString(CultureInfo.InvariantCulture));
            var row = rows[r];
            for (int c = 0; c < row.Length; c++)
                WriteCell(writer, r + 2, c + 1, row[c], isHeader: false);
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteCell(XmlWriter writer, int row, int col, object? value, bool isHeader)
    {
        string reference = GetColumnName(col) + row.ToString(CultureInfo.InvariantCulture);

        if (value == null)
            return;

        if (value is decimal or double or int or long)
        {
            string numStr = Convert.ToString(value, CultureInfo.InvariantCulture)!;
            writer.WriteStartElement("c");
            writer.WriteAttributeString("r", reference);
            if (!isHeader) writer.WriteAttributeString("s", "2");
            writer.WriteElementString("v", numStr);
            writer.WriteEndElement();
            return;
        }

        string text = value.ToString() ?? "";

        if (!isHeader && TryParseNumber(text, out double numVal))
        {
            writer.WriteStartElement("c");
            writer.WriteAttributeString("r", reference);
            writer.WriteAttributeString("s", "2");
            writer.WriteElementString("v", numVal.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();
            return;
        }

        writer.WriteStartElement("c");
        writer.WriteAttributeString("r", reference);
        if (isHeader) writer.WriteAttributeString("s", "1");
        writer.WriteAttributeString("t", "inlineStr");
        writer.WriteStartElement("is");
        writer.WriteStartElement("t");
        writer.WriteAttributeString("xml:space", "preserve");
        writer.WriteString(text);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static bool TryParseNumber(string text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var cleaned = new string(text.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
        return double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    private static string GetColumnName(int col)
    {
        string name = "";
        while (col > 0)
        {
            int rem = (col - 1) % 26;
            name = (char)('A' + rem) + name;
            col = (col - 1) / 26;
        }
        return name;
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }
}
