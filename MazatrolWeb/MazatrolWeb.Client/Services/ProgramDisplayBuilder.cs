namespace MazatrolWeb.Client.Services;

/// <summary>One cell in the legacy program grid (name/value + file metadata).</summary>
public sealed record LegacyCell(string Text, int FileOffset, string ParamType);

/// <summary>Title (green) or data (yellow) row in the program list.</summary>
public sealed class ProgramDisplayRow
{
    public required int LineNumber { get; init; }
    public required bool IsTitleRow { get; init; }
    public required ProgramBlock Block { get; init; }
    public required IReadOnlyList<LegacyCell> Cells { get; init; }
}

/// <summary>Builds program list rows for the Mazatrol program grid.</summary>
public static class ProgramDisplayBuilder
{
    public static IReadOnlyList<ProgramDisplayRow> BuildRows(IReadOnlyList<ProgramBlock> blocks)
    {
        var rows = new List<ProgramDisplayRow>();
        var lineNumber = 0;
        var lastCommand = string.Empty;

        foreach (var block in blocks)
        {
            var legacyRows = block.ToLegacyRows();
            var firstName = legacyRows[0].Name;

            if (firstName == "UNo")
            {
                lineNumber++;
                rows.Add(SpacerRow(lineNumber));
            }

            var showTitle = firstName == "UNo" || firstName != lastCommand;
            ProgramDisplayRow? titleRow = null;

            if (showTitle)
            {
                lineNumber++;
                titleRow = new ProgramDisplayRow
                {
                    LineNumber = lineNumber,
                    IsTitleRow = true,
                    Block = block,
                    Cells = BuildCells(legacyRows, useNames: true)
                };
                rows.Add(titleRow);
            }

            lastCommand = firstName;
            lineNumber++;
            rows.Add(new ProgramDisplayRow
            {
                LineNumber = lineNumber,
                IsTitleRow = false,
                Block = block,
                Cells = BuildCells(legacyRows, useNames: false)
            });
        }

        return rows;
    }

    private static ProgramDisplayRow SpacerRow(int lineNumber) => new()
    {
        LineNumber = lineNumber,
        IsTitleRow = false,
        Block = new ProgramBlock
        {
            UnitTypeId = 0,
            UnitName = string.Empty,
            UnitNumber = 0,
            UnitAddress = 0,
            IsUnitHeader = false,
            Parameters = []
        },
        Cells = [new LegacyCell(string.Empty, 0, string.Empty)]
    };

    private static IReadOnlyList<LegacyCell> BuildCells(IReadOnlyList<LegacyParameterRow> legacyRows, bool useNames)
    {
        return legacyRows.Select(row => new LegacyCell(
            useNames ? row.Name : row.ValueText,
            row.FileOffset,
            row.ParamType)).ToList();
    }
}

public sealed record LegacyParameterRow(string Name, string ValueText, int FileOffset, string ParamType);

public sealed partial class ProgramBlock
{
    /// <summary>Convert to legacy row format used by the original Mazatrol list control.</summary>
    public IReadOnlyList<LegacyParameterRow> ToLegacyRows()
    {
        var rows = new List<LegacyParameterRow>();
        if (IsUnitHeader)
        {
            rows.Add(new LegacyParameterRow("UNo", UnitNumber.ToString(), UnitAddress + 2, string.Empty));
            rows.Add(new LegacyParameterRow("UNIT", UnitName, UnitAddress, string.Empty));
        }
        else
        {
            rows.Add(new LegacyParameterRow(UnitName, UnitNumber.ToString(), UnitAddress + 2, string.Empty));
        }

        foreach (var param in Parameters)
        {
            rows.Add(new LegacyParameterRow(
                param.Name,
                param.DisplayValue,
                param.FileOffset,
                ParamTypeToLegacy(param.ParamType)));
        }

        while (rows.Count < 17)
            rows.Add(new LegacyParameterRow(string.Empty, string.Empty, 0, string.Empty));

        rows.Add(new LegacyParameterRow("addr", UnitAddress.ToString(), UnitAddress, string.Empty));
        return rows;
    }

    private static string ParamTypeToLegacy(ParameterType type) => type switch
    {
        ParameterType.NA => "NA",
        ParameterType.WholeNumber => "wholeNumber",
        ParameterType.ReadData => "readData",
        ParameterType.Text => "text",
        ParameterType.ReadFullNumber2B => "readFullNumber2B",
        ParameterType.ReadFullNumber1B => "readFullNumber1B",
        ParameterType.ReadLetter => "readLetter",
        ParameterType.ReadPattern => "readPattern",
        ParameterType.ReadPbdTool => "readPbdTool",
        ParameterType.ReadPbdMultiFlag => "readPbdMultiFlag",
        ParameterType.PartType => "partType",
        _ => "UNKNOWN"
    };
}
