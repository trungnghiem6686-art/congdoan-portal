using System.Text;

namespace CongDoan.Application.Shared;

/// <summary>CSV output that spreadsheet programs cannot turn into formulas (CSV injection).</summary>
public static class CsvSanitizer
{
    private static readonly char[] FormulaStart = { '=', '+', '-', '@', '\t', '\r' };

    public static string Escape(string? value)
    {
        var v = value ?? "";
        return v.Length > 0 && FormulaStart.Contains(v[0]) ? "'" + v : v;
    }

    public static string Line(IEnumerable<string?> cells) => string.Join(",", cells.Select(c =>
    {
        var v = Escape(c);
        return v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
    }));

    /// <summary>UTF-8 with BOM so Excel shows Vietnamese correctly.</summary>
    public static byte[] Document(IEnumerable<string?> header, IEnumerable<IEnumerable<string?>> rows)
    {
        var sb = new StringBuilder();
        sb.Append(Line(header)).Append("\r\n");
        foreach (var r in rows) sb.Append(Line(r)).Append("\r\n");
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }
}

/// <summary>Writes plain-text cells only (values are never interpreted as formulas).</summary>
public interface ISpreadsheetWriter
{
    byte[] Write(string sheetName, IReadOnlyList<string> header, IEnumerable<IReadOnlyList<object?>> rows);
}

public static class VietnamTime
{
    private static readonly TimeZoneInfo Zone = Find();

    private static TimeZoneInfo Find()
    {
        foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz)) return tz;
        return TimeZoneInfo.CreateCustomTimeZone("ICT", TimeSpan.FromHours(7), "Giờ Việt Nam", "Giờ Việt Nam");
    }

    public static DateTime FromUtc(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    public static string Format(DateTime? utc) => utc is null ? "" : FromUtc(utc.Value).ToString("dd/MM/yyyy HH:mm:ss");
}
