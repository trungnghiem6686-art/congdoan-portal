using ClosedXML.Excel;
using CongDoan.Application.Exams;

namespace CongDoan.Infrastructure.Exams;

public sealed class XlsxQuestionTemplate : IQuestionTemplate
{
    public IReadOnlyList<string[]> ReadXlsx(Stream xlsx)
    {
        using var book = new XLWorkbook(xlsx);
        var sheet = book.Worksheets.First();
        var used = sheet.RangeUsed();
        if (used is null) return Array.Empty<string[]>();
        var lastCol = used.LastColumn().ColumnNumber();
        var rows = new List<string[]>();
        for (var r = 1; r <= used.LastRow().RowNumber(); r++)
        {
            var row = new string[lastCol];
            // Formulas are read as their cached text, never evaluated from user input.
            for (var c = 1; c <= lastCol; c++) row[c - 1] = sheet.Cell(r, c).GetFormattedString();
            rows.Add(row);
        }
        return rows;
    }

    public byte[] Xlsx(IEnumerable<string[]> rows)
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("CauHoi");
        var header = QuestionImportService.Header;
        for (var c = 0; c < header.Length; c++) sheet.Cell(1, c + 1).Value = header[c];
        var r = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < row.Length; c++) sheet.Cell(r, c + 1).SetValue(row[c]);
            r++;
        }
        sheet.Row(1).Style.Font.Bold = true;
        sheet.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        book.SaveAs(ms);
        return ms.ToArray();
    }
}
