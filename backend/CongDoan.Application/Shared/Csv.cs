using System.Text;

namespace CongDoan.Application.Shared;

/// <summary>Minimal RFC 4180 reader (quoted fields, embedded commas/newlines, BOM).</summary>
public static class Csv
{
    public static List<string[]> Parse(string text)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        text = text.TrimStart('﻿');
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') inQuotes = false;
                else field.Append(c);
                continue;
            }
            switch (c)
            {
                case '"': inQuotes = true; break;
                case ',': row.Add(field.ToString()); field.Clear(); break;
                case '\r': break;
                case '\n':
                    row.Add(field.ToString()); field.Clear();
                    rows.Add(row.ToArray()); row.Clear();
                    break;
                default: field.Append(c); break;
            }
        }
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row.ToArray());
        }
        return rows;
    }

    /// <summary>Reads header-mapped rows; Row numbers are 1-based file lines (header = 1).</summary>
    public static IEnumerable<(int Row, Func<string, string> Get)> Records(string text)
    {
        var rows = Parse(text);
        if (rows.Count == 0) yield break;
        var header = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToArray();
        for (var r = 1; r < rows.Count; r++)
        {
            var values = rows[r];
            if (values.All(string.IsNullOrWhiteSpace)) continue;
            yield return (r + 1, col =>
            {
                var idx = Array.IndexOf(header, col);
                return idx >= 0 && idx < values.Length ? values[idx].Trim() : "";
            });
        }
    }
}
