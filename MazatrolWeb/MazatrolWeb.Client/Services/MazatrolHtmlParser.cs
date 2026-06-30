using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace MazatrolWeb.Client.Services;

/// <summary>Parses Mazatrol Matrix HTML program exports (PBF samples) into program blocks.</summary>
public static class MazatrolHtmlParser
{
    private static readonly Dictionary<string, int> UnitIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MAT"] = 1,
        ["END"] = 4,
        ["MANL PRG"] = 6,
        ["M-CODE"] = 7,
        ["HEAD"] = 19,
        ["BAR"] = 48,
        ["FACING"] = 51,
        ["THREAD"] = 52,
        ["T.GROOVE"] = 53,
        ["T.DRILL"] = 54,
        ["T.TAP"] = 55,
        ["POCKET"] = 99,
        ["DRILLING"] = 57,
        ["LINE"] = 58,
        ["TRANSFER"] = 59,
    };

    public static IReadOnlyList<ProgramBlock> Parse(string html)
    {
        return html.Contains("<table", StringComparison.OrdinalIgnoreCase)
            ? ParseTables(html)
            : ParsePreFormat(html);
    }

    private static IReadOnlyList<ProgramBlock> ParseTables(string html)
    {
        var blocks = new List<ProgramBlock>();
        var tablePattern = new Regex(@"<table[^>]*>(.*?)</table>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        ProgramBlock? pending = null;

        foreach (Match tableMatch in tablePattern.Matches(html))
        {
            var rows = ParseTableRows(tableMatch.Groups[1].Value);
            if (rows.Count == 0)
                continue;

            var header = rows[0];
            if (header.Count == 0)
                continue;

            if (header[0].Equals("UNo.", StringComparison.OrdinalIgnoreCase))
            {
                for (var r = 1; r < rows.Count; r++)
                {
                    var row = rows[r];
                    if (row.Count == 0 || !int.TryParse(row[0], out var unitNumber))
                        continue;

                    string unitName;
                    List<string> columns;
                    List<string> values;

                    if (header[1].StartsWith("MAT", StringComparison.OrdinalIgnoreCase))
                    {
                        unitName = "MAT";
                        columns = header.Skip(2).ToList();
                        values = row.Skip(1).ToList();
                    }
                    else
                    {
                        unitName = row.Count > 1 ? row[1].Trim() : "?";
                        columns = header.Skip(2).ToList();
                        values = row.Skip(2).ToList();
                    }

                    pending = CreateHeaderBlock(blocks.Count, unitNumber, unitName, columns, values);
                    blocks.Add(pending);
                }
                continue;
            }

            if (header[0].Equals("FIG", StringComparison.OrdinalIgnoreCase) && pending is not null)
            {
                for (var r = 1; r < rows.Count; r++)
                    blocks.Add(CreateSubBlock(blocks.Count, pending, "FIG", header.Skip(1).ToList(), rows[r]));
                continue;
            }

            if ((header[0].Equals("SNo.", StringComparison.OrdinalIgnoreCase)
                 || (header.Count > 1 && header[1].Equals("SNo.", StringComparison.OrdinalIgnoreCase)))
                && pending is not null)
            {
                var cols = header[0].Equals("SNo.", StringComparison.OrdinalIgnoreCase)
                    ? header.Skip(1).Where(c => !string.IsNullOrWhiteSpace(c)).ToList()
                    : header.Skip(2).ToList();

                for (var r = 1; r < rows.Count; r++)
                {
                    var row = rows[r];
                    var values = header[0].Equals("SNo.", StringComparison.OrdinalIgnoreCase)
                        ? row.Skip(1).ToList()
                        : row.Skip(2).ToList();
                    blocks.Add(CreateSubBlock(blocks.Count, pending, "SNo", cols, values));
                }
            }
        }

        return blocks;
    }

    private static List<List<string>> ParseTableRows(string tableHtml)
    {
        var rows = new List<List<string>>();
        var rowPattern = new Regex(@"<tr[^>]*>(.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var cellPattern = new Regex(@"<t[dh][^>]*>(.*?)</t[dh]>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match rowMatch in rowPattern.Matches(tableHtml))
        {
            var cells = new List<string>();
            foreach (Match cellMatch in cellPattern.Matches(rowMatch.Groups[1].Value))
            {
                var text = Regex.Replace(cellMatch.Groups[1].Value, "<[^>]+>", string.Empty);
                cells.Add(WebUtility.HtmlDecode(text).Trim());
            }
            if (cells.Count > 0)
                rows.Add(cells);
        }

        return rows;
    }

    private static IReadOnlyList<ProgramBlock> ParsePreFormat(string html)
    {
        var text = Regex.Replace(html, "<[^>]+>", string.Empty);
        text = WebUtility.HtmlDecode(text);
        var lines = text.Split('\n').Select(l => l.TrimEnd()).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        var blocks = new List<ProgramBlock>();
        ProgramBlock? pending = null;
        var i = 0;

        while (i < lines.Count)
        {
            if (!lines[i].StartsWith("UNo.", StringComparison.Ordinal))
            {
                i++;
                continue;
            }

            var header = Regex.Replace(lines[i], @"\s+", " ").Split(' ');
            i++;
            while (i < lines.Count && string.IsNullOrWhiteSpace(lines[i]))
                i++;
            if (i >= lines.Count)
                break;

            var parts = Regex.Split(lines[i].Trim(), @"\s{2,}|\s+");
            if (parts.Length == 0 || !int.TryParse(parts[0], out var unitNumber))
            {
                i++;
                continue;
            }

            var (unitName, columns, values) = InferPreUnit(header, parts);
            if (unitName == "?" || (unitName.Length > 0 && unitName.All(char.IsDigit)))
            {
                i++;
                continue;
            }

            pending = CreateHeaderBlock(blocks.Count, unitNumber, unitName, columns, values);
            blocks.Add(pending);
            i++;

            while (i < lines.Count)
            {
                var sub = lines[i].Trim();
                if (sub.StartsWith("UNo.", StringComparison.Ordinal))
                    break;
                if (sub.StartsWith("SNo.", StringComparison.Ordinal) || sub.StartsWith("FIG", StringComparison.Ordinal))
                {
                    var subHeader = Regex.Replace(sub, @"\s+", " ").Split(' ').ToList();
                    i++;
                    while (i < lines.Count)
                    {
                        var rowLine = lines[i].Trim();
                        if (rowLine.StartsWith("UNo.", StringComparison.Ordinal)
                            || rowLine.StartsWith("SNo.", StringComparison.Ordinal)
                            || rowLine.StartsWith("FIG", StringComparison.Ordinal))
                            break;
                        if (!string.IsNullOrWhiteSpace(rowLine) && pending is not null)
                        {
                            var rowValues = Regex.Split(rowLine, @"\s{2,}").Select(v => v.Trim()).ToList();
                            var kind = subHeader[0];
                            var cols = subHeader.Skip(1).Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
                            blocks.Add(CreateSubBlock(blocks.Count, pending, kind, cols, rowValues));
                        }
                        i++;
                    }
                    continue;
                }
                i++;
            }
        }

        return blocks;
    }

    private static (string UnitName, List<string> Columns, List<string> Values) InferPreUnit(
        string[] header,
        string[] parts)
    {
        var upper = header.Select(h => h.ToUpperInvariant().Replace(".", "")).ToArray();

        if (upper.Length > 1 && upper[1] == "MAT")
            return ("MAT", header.Skip(2).ToList(), parts.Skip(1).ToList());

        if (header.Any(h => h.Contains("TOOL", StringComparison.OrdinalIgnoreCase)))
            return ("MANL PRG", header.Skip(2).ToList(), parts.Skip(2).ToList());

        if (header.Any(h => h.Equals("M1", StringComparison.OrdinalIgnoreCase)))
            return ("M-CODE", header.Skip(2).ToList(), parts.Skip(2).ToList());

        if (header.Any(h => h.StartsWith("CONTI", StringComparison.OrdinalIgnoreCase)))
            return ("END", header.Skip(2).ToList(), parts.Skip(2).ToList());

        if (header.Any(h => h.Equals("HEAD", StringComparison.OrdinalIgnoreCase)))
            return ("HEAD", header.Skip(2).ToList(), parts.Skip(2).ToList());

        if (header.Any(h => h.Equals("DIA", StringComparison.OrdinalIgnoreCase))
            && header.Any(h => h.Equals("DEPTH", StringComparison.OrdinalIgnoreCase)))
            return ("DRILLING", header.Skip(2).ToList(), parts.Skip(2).ToList());

        if (header.Any(h => h.Equals("MODE", StringComparison.OrdinalIgnoreCase))
            && header.Any(h => h.Contains("POS-C", StringComparison.OrdinalIgnoreCase)))
            return ("POCKET", header.Skip(2).ToList(), parts.Skip(2).ToList());

        if (upper.Length > 2 && upper[1] == "UNIT" && upper[2] == "PART")
            return (parts.Length > 1 ? parts[1] : "?", header.Skip(3).ToList(), parts.Skip(2).ToList());

        if (parts.Length > 2 && parts[1] == "MANL" && parts[2] == "PRG")
            return ("MANL PRG", header.Skip(2).ToList(), parts.Skip(3).ToList());

        return (parts.Length > 1 ? parts[1] : "?", header.Skip(2).ToList(), parts.Skip(2).ToList());
    }

    private static ProgramBlock CreateHeaderBlock(
        int index,
        int unitNumber,
        string unitName,
        IReadOnlyList<string> columns,
        IReadOnlyList<string> values)
    {
        UnitIds.TryGetValue(unitName, out var unitTypeId);
        var address = MazatrolConstants.StartUnitAddress + index * MazatrolConstants.StandardUnitSize;
        var parameters = BuildParameters(columns, values, address);

        return new ProgramBlock
        {
            UnitTypeId = unitTypeId,
            UnitName = unitName,
            UnitNumber = unitNumber,
            UnitAddress = address,
            IsUnitHeader = true,
            Parameters = parameters
        };
    }

    private static ProgramBlock CreateSubBlock(
        int index,
        ProgramBlock parent,
        string kind,
        IReadOnlyList<string> columns,
        IReadOnlyList<string> values)
    {
        var address = MazatrolConstants.StartUnitAddress + index * MazatrolConstants.StandardUnitSize;
        var parameters = BuildParameters(columns, values, address);

        return new ProgramBlock
        {
            UnitTypeId = kind.Equals("FIG", StringComparison.OrdinalIgnoreCase) ? 168 : 180,
            UnitName = kind,
            UnitNumber = int.TryParse(values.FirstOrDefault(), out var n) ? n : 0,
            UnitAddress = address,
            IsUnitHeader = false,
            Parameters = parameters
        };
    }

    private static List<ParameterValue> BuildParameters(
        IReadOnlyList<string> columns,
        IReadOnlyList<string> values,
        int baseAddress)
    {
        var parameters = new List<ParameterValue>();
        for (var i = 0; i < columns.Count && i < values.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(columns[i]))
                continue;

            parameters.Add(new ParameterValue
            {
                Name = columns[i],
                Value = ParseValue(values[i]),
                FileOffset = baseAddress + 8 + i * 4,
                ParamType = InferType(values[i])
            });
        }

        return parameters;
    }

    private static object ParseValue(string raw)
    {
        if (raw is "@" or "?" or "")
            return raw;
        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
            return f;
        return raw;
    }

    private static ParameterType InferType(string raw)
    {
        if (raw is "@" or "?")
            return ParameterType.NA;
        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            return ParameterType.ReadData;
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            return ParameterType.ReadFullNumber2B;
        return ParameterType.Text;
    }
}
