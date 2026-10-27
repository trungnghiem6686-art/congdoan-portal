using ClosedXML.Excel;
using CongDoan.Application.Shared;

namespace CongDoan.Infrastructure.Shared;

public sealed class XlsxWriter : ISpreadsheetWriter
{
    public byte[] Write(string sheetName, IReadOnlyList<string> header, IEnumerable<IReadOnlyList<object?>> rows)
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet(sheetName);
        for (var c = 0; c < header.Count; c++) sheet.Cell(1, c + 1).SetValue(header[c]);
        var r = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < row.Count; c++)
            {
                var cell = sheet.Cell(r, c + 1);
                switch (row[c])
                {
                    case null: break;
                    case decimal d: cell.SetValue(d); break;
                    case int i: cell.SetValue(i); break;
                    // SetValue(string) stores text; a leading "=" is never treated as a formula.
                    default: cell.SetValue(row[c]!.ToString()); break;
                }
            }
            r++;
        }
        sheet.Row(1).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents(1, Math.Min(r, 200));
        using var ms = new MemoryStream();
        book.SaveAs(ms);
        return ms.ToArray();
    }
}
