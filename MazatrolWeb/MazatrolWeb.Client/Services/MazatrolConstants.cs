namespace MazatrolWeb.Client.Services;

/// <summary>Mazatrol binary layout and supported unit type identifiers.</summary>
public static class MazatrolConstants
{
    public const int StartUnitAddress = 0xFC;
    public const int StandardUnitSize = 100;
    public const int FacingUnitSize = 400;
    public const int EndUnitTypeId = 4;

    public const string StructureXmlPath = "data/qts200m.xml";

    public static readonly HashSet<int> DisplayedUnitTypeIds =
    [
        1, 4, 6, 48, 51, 52, 53, 54, 161, 168, 170, 171, 172, 173, 180
    ];

    public static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".pbg", ".pbf", ".pbd", ".pbe", ".pbm", ".mzk", ".t6m", ".m6m", ".maz"
        };

    public static readonly IReadOnlyDictionary<string, (string FileName, int Size)> UnitTemplates =
        new Dictionary<string, (string, int)>(StringComparer.OrdinalIgnoreCase)
        {
            ["LIN"] = ("LIN.unit", StandardUnitSize),
            ["TPR"] = ("TPR.unit", StandardUnitSize),
            ["FACING"] = ("FACING.unit", FacingUnitSize),
        };
}
