using System.ComponentModel.DataAnnotations;

namespace MazatrolWeb.Client.Services;

/// <summary>Parameter storage types from structure XML definitions.</summary>
public enum ParameterType
{
    NA,
    WholeNumber,
    ReadData,
    Text,
    ReadFullNumber2B,
    ReadFullNumber1B,
    ReadLetter,
    ReadPattern,
    ReadPbdTool,
    ReadPbdMultiFlag,
    PartType,
    Unknown
}

/// <summary>Unit-level edit operations.</summary>
public enum UnitEditAction
{
    Delete,
    Duplicate,
    Export,
    InsertLin,
    InsertTpr,
    InsertFacing
}

public sealed record PatternOption(string Name, int Value);

public sealed record ParameterDefinition(
    string Name,
    int Offset,
    ParameterType ParamType,
    IReadOnlySet<string> VisibleFor,
    IReadOnlyList<PatternOption> PatternOptions);

public sealed record UnitDefinition(
    int UnitId,
    string Name,
    IReadOnlyList<ParameterDefinition> Parameters,
    bool IsSubUnit)
{
    public bool IsFigure => Name == "FIG";
    public bool IsSequenceNumber => Name == "SNo";
}

public sealed class ParameterValue
{
    public required string Name { get; init; }
    public required object Value { get; set; }
    public required int FileOffset { get; init; }
    public required ParameterType ParamType { get; init; }
    public bool IsDefined { get; init; } = true;
    public string DisplayValue =>
        MazatrolParameterFormatter.Format(Value, ParamType, IsDefined);
    public bool IsEditable => ParamType == ParameterType.ReadData && IsDefined;
}

public sealed partial class ProgramBlock
{
    public required int UnitTypeId { get; init; }
    public required string UnitName { get; init; }
    public required int UnitNumber { get; init; }
    public required int UnitAddress { get; init; }
    public required bool IsUnitHeader { get; init; }
    public required List<ParameterValue> Parameters { get; init; }

    public string Id => $"0x{UnitAddress:X}-{UnitName}";

    public object? Get(string name)
    {
        foreach (var p in Parameters)
        {
            if (p.Name == name)
                return p.Value;
        }
        return null;
    }
}

public sealed record MaterialStock(double Od, double InnerDiameter, double Length, double Workface);

public sealed record FacingCut(double FinishX, double FinishZ);

public sealed record BarFigure(
    int LineNumber,
    double StartX,
    double StartZ,
    double FinishX,
    double FinishZ,
    double StartCorner,
    double FinishCorner);

public sealed class TurningSimulationInput
{
    public MaterialStock? Stock { get; set; }
    public FacingCut? Facing { get; set; }
    public List<BarFigure> BarFigures { get; } = [];
}

/// <summary>Lathe profile point for Three.js (radius, axial position).</summary>
public sealed record ProfilePoint(double Radius, double AxialZ);

/// <summary>DTO passed to JavaScript for mesh generation.</summary>
public sealed record SimulationMeshDto
{
    public double StockOd { get; init; }
    public double StockId { get; init; }
    public double StockLength { get; init; }
    public double Workface { get; init; }
    public FacingCut? Facing { get; init; }
    public IReadOnlyList<ProfilePoint> Profile { get; init; } = [];
}

/// <summary>Editable parameter form model with validation.</summary>
public sealed class EditableParameterModel
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Value is required.")]
    public string Value { get; set; } = string.Empty;

    public int FileOffset { get; set; }
    public ParameterType ParamType { get; set; }
}
